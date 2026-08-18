using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.Repository.Services.Payout;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout;

public class PayoutOrchestrator : IPayoutOrchestrator
{
    private readonly AppDbContext _db;
    private readonly IPayoutGatewayResolver _resolver;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICurrentUserService _currentUser;
    private readonly IPayoutReceiptService _receipts;

    public PayoutOrchestrator(
        AppDbContext db,
        IPayoutGatewayResolver resolver,
        IDateTimeProvider dateTime,
        ICurrentUserService currentUser,
        IPayoutReceiptService receipts)
    {
        _db = db;
        _resolver = resolver;
        _dateTime = dateTime;
        _currentUser = currentUser;
        _receipts = receipts;
    }

    public async Task<Result<PayoutResult>> ExecutePayoutAsync(string memberId, DateTime processDate, CancellationToken ct = default)
    {
        var now = _dateTime.Now;
        var actor = _currentUser.UserId;

        // 1. Preferred active wallet → gateway.
        var wallet = await _db.Wallets
            .Where(w => w.MemberId == memberId && !w.IsDeleted && w.Status == WalletStatus.Approved)
            .OrderByDescending(w => w.IsPreferred)
            .FirstOrDefaultAsync(ct);

        if (wallet is null)
            return Result<PayoutResult>.Failure("NO_PAYOUT_WALLET", $"Member {memberId} has no approved payout wallet");

        if (string.IsNullOrWhiteSpace(wallet.AccountIdentifier))
            return Result<PayoutResult>.Failure("NO_PAYOUT_ACCOUNT", $"Member {memberId} wallet has no account identifier");

        var gatewayResult = _resolver.Resolve(wallet.WalletType);
        if (!gatewayResult.IsSuccess)
            return Result<PayoutResult>.Failure(gatewayResult.ErrorCode!, gatewayResult.Error!);
        var gateway = gatewayResult.Value!;

        // 2. Candidate earnings due on/before processDate, excluding already-reserved ones.
        var reservedEarningIds = _db.PayoutAttemptEarnings
            .Where(pae => _db.PayoutAttempts.Any(a => a.Id == pae.PayoutAttemptId && a.Outcome != PayoutOutcome.Failed))
            .Select(pae => pae.CommissionEarningId);

        var earnings = await _db.CommissionEarnings
            .Where(e => e.BeneficiaryMemberId == memberId
                        && e.Status == CommissionEarningStatus.Pending
                        && e.PaymentDate <= processDate
                        && !e.IsDeleted
                        && !reservedEarningIds.Contains(e.Id))
            .OrderBy(e => e.PaymentDate)
            .ToListAsync(ct);

        if (earnings.Count == 0)
            return Result<PayoutResult>.Success(new PayoutResult
            {
                MemberId = memberId, Outcome = PayoutOutcome.Pending, AmountUsd = 0m, EarningsCount = 0
            });

        var amount = earnings.Sum(e => e.Amount);

        // 3. Create the immutable attempt (snapshot frozen now) + reserve earnings.
        var attempt = new PayoutAttempt
        {
            MemberId = memberId,
            WalletTypeSnapshot = wallet.WalletType,
            PayoutAccountSnapshot = wallet.AccountIdentifier!,
            AmountUsd = amount,
            ProcessDateUtc = processDate,
            Outcome = PayoutOutcome.Pending,
            AttemptedAtUtc = now,
            EarningsCount = earnings.Count,
            DisbursementMode = DisbursementMode.Online,
            CreationDate = now,
            CreatedBy = actor
        };
        _db.PayoutAttempts.Add(attempt);
        await _db.SaveChangesAsync(ct); // get attempt.Id

        foreach (var e in earnings)
            _db.PayoutAttemptEarnings.Add(new PayoutAttemptEarning
            {
                PayoutAttemptId = attempt.Id, CommissionEarningId = e.Id, Amount = e.Amount,
                CreationDate = now, CreatedBy = actor
            });
        await _db.SaveChangesAsync(ct);

        var accountCtx = new PayoutAccountContext
        {
            MemberId = memberId, WalletType = wallet.WalletType, AccountIdentifier = wallet.AccountIdentifier!
        };

        // 4. Lazy subscribe: validate, then subscribe if missing.
        var validate = await TimedAsync(() => gateway.ValidateAccountAsync(accountCtx, ct));
        await LogApiAsync(memberId, wallet.WalletType, "ValidateAccount", validate.Result.IsSuccess,
            validate.Result.IsSuccess ? null : validate.Result.Error, validate.ElapsedMs, now, actor, ct);

        if (validate.Result.IsSuccess && !validate.Result.Value!.Exists)
        {
            var subscribe = await TimedAsync(() => gateway.SubscribeAccountAsync(accountCtx, ct));
            await LogApiAsync(memberId, wallet.WalletType, "SubscribeAccount", subscribe.Result.IsSuccess,
                subscribe.Result.IsSuccess ? null : subscribe.Result.Error, subscribe.ElapsedMs, now, actor, ct);

            if (!subscribe.Result.IsSuccess)
                return await FailAsync(attempt, subscribe.Result.ErrorCode, subscribe.Result.Error, now, ct);
        }
        else if (!validate.Result.IsSuccess)
        {
            return await FailAsync(attempt, validate.Result.ErrorCode, validate.Result.Error, now, ct);
        }

        // 5. Disburse.
        var transferCtx = new PayoutTransferContext
        {
            MemberId = memberId, WalletType = wallet.WalletType, AccountIdentifier = wallet.AccountIdentifier!,
            AmountUsd = amount, Reference = attempt.Id.ToString()
        };
        var transfer = await TimedAsync(() => gateway.DisburseAsync(transferCtx, ct));
        await LogApiAsync(memberId, wallet.WalletType, "Disburse", transfer.Result.IsSuccess,
            transfer.Result.IsSuccess ? null : transfer.Result.Error, transfer.ElapsedMs, now, actor, ct);

        if (!transfer.Result.IsSuccess)
            return await FailAsync(attempt, transfer.Result.ErrorCode, transfer.Result.Error, now, ct);

        // 6. Success → finalize (shared path; Sprint 19 reuses this for CSV reconciliation).
        await FinalizeSuccessAsync(attempt, transfer.Result.Value!.GatewayTransactionId, transfer.ElapsedMs, ct);

        return Result<PayoutResult>.Success(new PayoutResult
        {
            MemberId = memberId, Outcome = PayoutOutcome.Success, AmountUsd = amount,
            EarningsCount = earnings.Count, PayoutAttemptId = attempt.Id
        });
    }

    public async Task FinalizeSuccessAsync(PayoutAttempt attempt, string? gatewayTxnId, long? latencyMs, CancellationToken ct = default)
    {
        var now = _dateTime.Now;
        var actor = _currentUser.UserId;

        var earningIds = await _db.PayoutAttemptEarnings
            .Where(x => x.PayoutAttemptId == attempt.Id)
            .Select(x => x.CommissionEarningId)
            .ToListAsync(ct);

        var earnings = await _db.CommissionEarnings.Where(e => earningIds.Contains(e.Id)).ToListAsync(ct);
        foreach (var e in earnings)
        {
            e.Status = CommissionEarningStatus.Paid;
            e.LastUpdateDate = now;
            e.LastUpdateBy = actor;
        }

        attempt.Outcome = PayoutOutcome.Success;
        attempt.GatewayTransactionId = gatewayTxnId;
        attempt.CompletedAtUtc = now;
        attempt.LatencyMs = latencyMs;
        attempt.LastUpdateDate = now;
        attempt.LastUpdateBy = actor;

        await _db.SaveChangesAsync(ct);
        await _receipts.IssueReceiptAsync(attempt, ct); // best-effort; never throws
    }

    private async Task<Result<PayoutResult>> FailAsync(
        PayoutAttempt attempt, string? code, string? message, DateTime now, CancellationToken ct)
    {
        attempt.Outcome = PayoutOutcome.Failed;
        attempt.GatewayErrorCode = code;
        attempt.GatewayErrorMessage = message;
        attempt.CompletedAtUtc = now;
        attempt.LastUpdateDate = now;
        attempt.LastUpdateBy = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);

        return Result<PayoutResult>.Success(new PayoutResult
        {
            MemberId = attempt.MemberId, Outcome = PayoutOutcome.Failed, AmountUsd = attempt.AmountUsd,
            EarningsCount = attempt.EarningsCount, PayoutAttemptId = attempt.Id,
            GatewayErrorCode = code, GatewayErrorMessage = message
        });
    }

    private async Task LogApiAsync(
        string memberId, WalletType walletType, string operation, bool success,
        string? error, long durationMs, DateTime now, string actor, CancellationToken ct)
    {
        _db.WalletApiLogs.Add(new MemberWalletApiLog
        {
            MemberId = memberId,
            WalletType = walletType,
            Operation = operation,
            HttpStatusCode = success ? 200 : 0,
            Success = success,
            ErrorMessage = error,
            DurationMs = (int)durationMs,
            CreationDate = now,
            CreatedBy = actor
        });
        await _db.SaveChangesAsync(ct);
    }

    private static async Task<(T Result, long ElapsedMs)> TimedAsync<T>(Func<Task<T>> action)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await action();
        sw.Stop();
        return (result, sw.ElapsedMilliseconds);
    }
}
