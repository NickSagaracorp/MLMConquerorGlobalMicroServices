using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.CommissionEngine.Jobs;

/// <summary>
/// HangFire recurring job — Weekly on Mondays at 4:00 AM UTC, queue "commissions".
///
/// For each member with pending DailyResidualEarning rows:
///   - If the sum &gt;= DailyResidualConsolidationMinimum (from GlobalParameters):
///     → mark those rows Paid, set ConsolidatedIntoCommissionEarningId,
///       PaymentDate, CommentedBy, and PaymentComment;
///       and create one CommissionEarning credit row.
///   - Below the minimum → leave pending for the next Monday.
///
/// Idempotent: only processes Pending rows; Paid rows are excluded.
/// </summary>
[Queue("commissions")]
public class DailyResidualConsolidationJob
{
    private const string ConsolidationMinimumKey = "DailyResidualConsolidationMinimum";
    private const decimal DefaultMinimum = 100m;
    private const string Actor = "weekly-consolidation";

    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ILogger<DailyResidualConsolidationJob> _logger;

    public DailyResidualConsolidationJob(
        AppDbContext db,
        IDateTimeProvider dateTime,
        ILogger<DailyResidualConsolidationJob> logger)
    {
        _db       = db;
        _dateTime = dateTime;
        _logger   = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now = _dateTime.Now;
        _logger.LogInformation("DailyResidualConsolidationJob: starting at {Time}.", now);

        var minimum = await GetConsolidationMinimumAsync(ct);
        var dailyResidualTypeId = await GetDailyResidualCommissionTypeIdAsync(ct);

        // Group pending DailyResidualEarning rows by member
        var memberIds = await _db.DailyResidualEarnings
            .AsNoTracking()
            .Where(e => e.Status == CommissionEarningStatus.Pending)
            .Select(e => e.BeneficiaryMemberId)
            .Distinct()
            .ToListAsync(ct);

        _logger.LogInformation(
            "DailyResidualConsolidationJob: {Count} members with pending rows.", memberIds.Count);

        int consolidated = 0, skipped = 0;

        foreach (var memberId in memberIds)
        {
            try
            {
                var pendingRows = await _db.DailyResidualEarnings
                    .Where(e => e.BeneficiaryMemberId == memberId
                             && e.Status == CommissionEarningStatus.Pending)
                    .ToListAsync(ct);

                var total = pendingRows.Sum(e => e.Amount);
                if (total < minimum)
                {
                    skipped++;
                    continue;
                }

                // Create one consolidated CommissionEarning credit
                var earningRow = new CommissionEarning
                {
                    BeneficiaryMemberId = memberId,
                    CommissionTypeId    = dailyResidualTypeId,
                    Amount              = total,
                    Status              = CommissionEarningStatus.Pending,
                    EarnedDate          = now,
                    PaymentDate         = now,
                    IsManualEntry       = false,
                    Notes               = $"Weekly Daily Residual consolidation — {pendingRows.Count} rows, total {total:C}.",
                    CreatedBy           = Actor,
                    CreationDate        = now,
                    LastUpdateDate      = now
                };
                _db.CommissionEarnings.Add(earningRow);
                await _db.SaveChangesAsync(ct); // get the Id before updating references

                // Mark all pending DailyResidualEarning rows as Paid and record payment tracking
                foreach (var row in pendingRows)
                {
                    row.Status = CommissionEarningStatus.Paid;
                    row.ConsolidatedIntoCommissionEarningId = earningRow.Id;
                    row.PaymentDate    = now;
                    row.CommentedBy    = Actor;
                    row.PaymentComment = $"Consolidated into CommissionEarning #{earningRow.Id} by the weekly daily-residual consolidation job";
                }

                await _db.SaveChangesAsync(ct);
                consolidated++;

                _logger.LogInformation(
                    "DailyResidualConsolidationJob: consolidated {Count} rows (total {Total:C}) for member {MemberId}.",
                    pendingRows.Count, total, memberId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "DailyResidualConsolidationJob: error consolidating for member {MemberId}.", memberId);
            }
        }

        _logger.LogInformation(
            "DailyResidualConsolidationJob: completed — consolidated={Consolidated}, skipped={Skipped}.",
            consolidated, skipped);
    }

    private async Task<decimal> GetConsolidationMinimumAsync(CancellationToken ct)
    {
        var param = await _db.GlobalParameters
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Key == ConsolidationMinimumKey, ct);

        if (param is not null && decimal.TryParse(param.Value, out var parsed))
            return parsed;

        return DefaultMinimum;
    }

    private async Task<int> GetDailyResidualCommissionTypeIdAsync(CancellationToken ct)
    {
        var type = await _db.CommissionTypes
            .AsNoTracking()
            .Where(t => t.ResidualBased && !t.IsPaidOnSignup && t.IsActive)
            .OrderBy(t => t.Id)
            .FirstOrDefaultAsync(ct);

        return type?.Id ?? 1;
    }
}
