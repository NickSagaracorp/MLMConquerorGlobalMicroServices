using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Recurring;

public class CommissionBalanceService : ICommissionBalanceService
{
    private const string ConsolidationMinimumKey = "DailyResidualConsolidationMinimum";
    private const decimal DefaultConsolidationMinimum = 100m;

    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;

    public CommissionBalanceService(AppDbContext db, IDateTimeProvider dateTime)
    {
        _db       = db;
        _dateTime = dateTime;
    }

    // ── GetAvailableAsync ────────────────────────────────────────────────────

    public async Task<CommissionBalanceSummary> GetAvailableAsync(
        string memberId, CancellationToken ct = default)
    {
        var minimum = await GetConsolidationMinimumAsync(ct);

        var generalPending = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(e => e.BeneficiaryMemberId == memberId && e.Status == CommissionEarningStatus.Pending)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

        var dailyResidualPending = await _db.DailyResidualEarnings
            .AsNoTracking()
            .Where(e => e.BeneficiaryMemberId == memberId && e.Status == CommissionEarningStatus.Pending)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

        var eligibleDailyResidual = dailyResidualPending >= minimum ? dailyResidualPending : 0m;
        var available = generalPending + eligibleDailyResidual;

        return new CommissionBalanceSummary
        {
            GeneralPending        = generalPending,
            DailyResidualPending  = dailyResidualPending,
            EligibleDailyResidual = eligibleDailyResidual,
            Available             = available,
            ConsolidationMinimum  = minimum
        };
    }

    // ── ConsolidateDailyResidualAsync ─────────────────────────────────────────

    public Task<string?> ConsolidateDailyResidualAsync(
        string memberId, string actor, CancellationToken ct = default)
        => ConsolidateCoreAsync(memberId, actor, paymentComment: null, ct);

    /// <summary>
    /// Core consolidation logic shared by the public API path and the internal
    /// FundWithCommissionBalanceAsync path (which can supply a richer payment comment).
    /// </summary>
    private async Task<string?> ConsolidateCoreAsync(
        string memberId, string actor, string? paymentComment, CancellationToken ct)
    {
        var minimum = await GetConsolidationMinimumAsync(ct);

        var pendingRows = await _db.DailyResidualEarnings
            .Where(e => e.BeneficiaryMemberId == memberId && e.Status == CommissionEarningStatus.Pending)
            .ToListAsync(ct);

        var total = pendingRows.Sum(e => e.Amount);
        if (total < minimum)
            return null; // below the minimum — leave pending

        var now = _dateTime.Now;

        // Find the "Daily Residual" CommissionType so we can tag the consolidated row correctly.
        var dailyResidualTypeId = await GetDailyResidualCommissionTypeIdAsync(ct);

        // Create one consolidated CommissionEarning credit
        var consolidated = new CommissionEarning
        {
            BeneficiaryMemberId = memberId,
            CommissionTypeId    = dailyResidualTypeId,
            Amount              = total,
            Status              = CommissionEarningStatus.Pending,
            EarnedDate          = now,
            PaymentDate         = now,
            IsManualEntry       = false,
            Notes               = $"Daily Residual consolidation — {pendingRows.Count} rows consolidated.",
            CreatedBy           = actor,
            CreationDate        = now,
            LastUpdateDate      = now
        };
        _db.CommissionEarnings.Add(consolidated);
        await _db.SaveChangesAsync(ct); // get the Id before updating references

        // Mark all pending DailyResidualEarning rows as Paid and record payment tracking
        var resolvedComment = paymentComment
            ?? $"Consolidated into CommissionEarning #{consolidated.Id} by the ad-hoc daily-residual consolidation";

        foreach (var row in pendingRows)
        {
            row.Status = CommissionEarningStatus.Paid;
            row.ConsolidatedIntoCommissionEarningId = consolidated.Id;
            row.PaymentDate    = now;
            row.CommentedBy    = actor;
            row.PaymentComment = resolvedComment;
        }

        await _db.SaveChangesAsync(ct);
        return consolidated.Id;
    }

    // ── FundWithCommissionBalanceAsync ────────────────────────────────────────

    public async Task<Result<CommissionFundResult>> FundWithCommissionBalanceAsync(
        string memberId,
        RecurringBillingPlan plan,
        int? tokenTypeIdOverride,
        decimal amountDue,
        string orderId,
        string productId,
        string actor,
        CancellationToken ct = default)
    {
        var now = _dateTime.Now;

        // 1. Consolidate daily residual if eligible.
        // Use the richer comment path so the PaymentComment on each DailyResidualEarning row
        // captures the order context at consolidation time (the token Tx Id is not yet known).
        var consolidatedEarningId = await ConsolidateCoreAsync(
            memberId, actor,
            paymentComment: $"Consolidated to fund membership-token-purchase for renewal order #{orderId}",
            ct);

        // 2. Create a negative CommissionEarning debit
        var dailyResidualTypeId = await GetDailyResidualCommissionTypeIdAsync(ct);
        var debit = new CommissionEarning
        {
            BeneficiaryMemberId = memberId,
            CommissionTypeId    = dailyResidualTypeId,
            Amount              = -amountDue,
            Status              = CommissionEarningStatus.Pending,
            EarnedDate          = now,
            PaymentDate         = now,
            SourceOrderId       = orderId,
            Notes               = $"Recurring billing debit — {amountDue:C} — Order {orderId}",
            IsManualEntry       = false,
            CreatedBy           = actor,
            CreationDate        = now,
            LastUpdateDate      = now
        };
        _db.CommissionEarnings.Add(debit);
        await _db.SaveChangesAsync(ct);

        // 3. Determine the effective token type (per-product override > plan-level)
        var effectiveTokenTypeId = tokenTypeIdOverride ?? plan.TokenTypeId;
        if (effectiveTokenTypeId is null)
            return Result<CommissionFundResult>.Failure("NO_TOKEN_TYPE",
                $"Plan '{plan.Name}' has no TokenTypeId configured for commission-balance funding.");

        // 4. Issue a TokenTransaction (Quantity 1) and bump TokenBalance
        var tokenTx = new TokenTransaction
        {
            MemberId        = memberId,
            TokenTypeId     = effectiveTokenTypeId.Value,
            TransactionType = TokenTransactionType.EarnedRenewal,
            Quantity        = 1,
            Notes           = $"Recurring plan renewal — {plan.Name} — Order {orderId}",
            CreatedBy       = actor,
            CreationDate    = now,
            OriginalOwnerMemberId = memberId,
            Status          = TokenInstanceStatus.Issued
        };
        _db.TokenTransactions.Add(tokenTx);

        var balance = await _db.TokenBalances
            .FirstOrDefaultAsync(b => b.MemberId == memberId && b.TokenTypeId == effectiveTokenTypeId.Value, ct);

        if (balance is null)
        {
            balance = new TokenBalance
            {
                MemberId      = memberId,
                TokenTypeId   = effectiveTokenTypeId.Value,
                Balance       = 0,
                CreatedBy     = actor,
                CreationDate  = now,
                LastUpdateDate = now
            };
            _db.TokenBalances.Add(balance);
        }

        balance.Add(1);
        balance.LastUpdateDate = now;
        balance.LastUpdateBy   = actor;

        // 5. Mark the Order as Paid and create PaymentHistory
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is not null)
        {
            order.Status         = OrderStatus.Paid;
            order.LastUpdateDate = now;
            order.LastUpdateBy   = actor;
        }

        var paymentHistory = new PaymentHistory
        {
            OrderId              = orderId,
            MemberId             = memberId,
            Amount               = amountDue,
            GatewayName          = "CommissionBalance",
            GatewayTransactionId = debit.Id, // reference the debit row
            TransactionStatus    = PaymentHistoryTransactionStatus.Captured,
            ProcessedAt          = now,
            CreationDate         = now,
            CreatedBy            = actor,
            LastUpdateDate       = now,
            LastUpdateBy         = actor
        };
        _db.PaymentHistories.Add(paymentHistory);

        await _db.SaveChangesAsync(ct);

        // Now that tokenTx.Id is generated, backfill the full PaymentComment on the consolidated rows.
        if (consolidatedEarningId is not null)
        {
            var consolidatedRows = await _db.DailyResidualEarnings
                .Where(e => e.ConsolidatedIntoCommissionEarningId == consolidatedEarningId)
                .ToListAsync(ct);

            var fullComment = $"Consolidated into CommissionEarning #{consolidatedEarningId} to fund token #{tokenTx.Id} for renewal order #{orderId}";
            foreach (var row in consolidatedRows)
                row.PaymentComment = fullComment;

            await _db.SaveChangesAsync(ct);
        }

        return Result<CommissionFundResult>.Success(new CommissionFundResult
        {
            CommissionDebitEarningId = debit.Id,
            TokenTransactionId       = tokenTx.Id,
            PaymentHistoryId         = paymentHistory.Id,
            ConsolidatedEarningId    = consolidatedEarningId
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<decimal> GetConsolidationMinimumAsync(CancellationToken ct)
    {
        var param = await _db.GlobalParameters
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Key == ConsolidationMinimumKey, ct);

        if (param is not null && decimal.TryParse(param.Value, out var parsed))
            return parsed;

        return DefaultConsolidationMinimum;
    }

    private async Task<int> GetDailyResidualCommissionTypeIdAsync(CancellationToken ct)
    {
        // Find a commission type that is residual-based and not paid on signup
        var type = await _db.CommissionTypes
            .AsNoTracking()
            .Where(t => t.ResidualBased && !t.IsPaidOnSignup && t.IsActive)
            .OrderBy(t => t.Id)
            .FirstOrDefaultAsync(ct);

        return type?.Id ?? 1; // fallback to Id=1 if none found (graceful degradation)
    }
}
