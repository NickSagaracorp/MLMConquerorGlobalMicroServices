using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateDailyResidual;

// NOTE: As of the RecurringBillingEngine sprint, new daily-residual accruals are written to
// DailyResidualEarning (not CommissionEarning). The weekly DailyResidualConsolidationJob
// aggregates pending DailyResidualEarning rows into a single CommissionEarning credit when
// the member's balance exceeds DailyResidualConsolidationMinimum. Existing CommissionEarning
// rows with the residual type are left untouched (no backfill — handled separately by
// the AddDailyResidualEarningSnapshotFields migration).

public class CalculateDailyResidualHandler
    : IRequestHandler<CalculateDailyResidualCommand, Result<CalculationResultResponse>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICurrentUserService _currentUser;

    public CalculateDailyResidualHandler(AppDbContext db, IDateTimeProvider dateTime, ICurrentUserService currentUser)
    {
        _db = db;
        _dateTime = dateTime;
        _currentUser = currentUser;
    }

    public async Task<Result<CalculationResultResponse>> Handle(
        CalculateDailyResidualCommand command, CancellationToken ct)
    {
        var now = _dateTime.Now;
        var periodDate = (command.PeriodDate ?? now).Date;

        // Load active residual commission types (binary, not paid on signup)
        var residualTypes = await _db.CommissionTypes
            .AsNoTracking()
            .Where(t => t.IsActive && t.ResidualBased && !t.IsPaidOnSignup)
            .ToListAsync(ct);

        if (residualTypes.Count == 0)
            return Result<CalculationResultResponse>.Failure(
                "NO_RESIDUAL_TYPES", "No active residual commission types configured.");

        // Guard against double-run for the same period
        // Now checks DailyResidualEarning (new table) instead of CommissionEarning.
        var alreadyRan = await _db.DailyResidualEarnings
            .AnyAsync(e => e.EarnedDate.Date == periodDate, ct);

        if (alreadyRan)
            return Result<CalculationResultResponse>.Failure(
                "ALREADY_CALCULATED",
                $"Daily residual for period {periodDate:yyyy-MM-dd} was already calculated.");

        // Single bulk query: stats for all active ambassadors.
        // Include PersonalPoints for the snapshot field alongside DualTeamPoints and EnrollmentPoints.
        var stats = await (
            from s in _db.MemberStatistics.AsNoTracking()
            join m in _db.MemberProfiles.AsNoTracking()
                on s.MemberId equals m.MemberId
            where m.MemberType == Domain.Enums.MemberType.Ambassador
               && m.Status == Domain.Entities.Member.MemberAccountStatus.Active
               && (s.DualTeamPoints > 0 || s.EnrollmentPoints > 0)
            select new
            {
                s.MemberId,
                s.DualTeamPoints,
                s.EnrollmentPoints,
                s.PersonalPoints
            }
        ).ToListAsync(ct);

        if (stats.Count == 0)
            return Result<CalculationResultResponse>.Success(new CalculationResultResponse
            {
                CommissionType = "DailyResidual",
                RecordsCreated = 0,
                TotalAmountCalculated = 0,
                PeriodDate = periodDate,
                SkippedReasons = new() { "No active ambassadors with qualifying points found." }
            });

        // Load each qualifying member's current rank in one bulk query.
        // "Current rank" = the highest-SortOrder MemberRankHistory entry for the member.
        // We join to RankDefinition to get the RankDefinitionId (which is the FK used as CurrentRankId).
        var memberIds = stats.Select(s => s.MemberId).ToList();

        // Subquery: for each memberId, get the RankDefinitionId of the row with the highest SortOrder.
        // In SQL terms: GROUP BY MemberId, pick MAX(SortOrder), join back to get the Id.
        // We do this in two steps to avoid EF translation issues with group-max patterns.
        var rankHistories = await _db.MemberRankHistories
            .AsNoTracking()
            .Include(h => h.RankDefinition)
            .Where(h => memberIds.Contains(h.MemberId) && !h.IsDeleted && h.RankDefinition != null)
            .ToListAsync(ct);

        // Build a dictionary: memberId → highest-SortOrder RankDefinitionId (nullable)
        var memberCurrentRankId = rankHistories
            .GroupBy(h => h.MemberId)
            .ToDictionary(
                g => g.Key,
                g => (int?)g.OrderByDescending(h => h.RankDefinition!.SortOrder).First().RankDefinitionId);

        // For each ambassador: find the HIGHEST qualifying DTR tier and pay that fixed daily amount.
        // An ambassador qualifies at exactly one tier per day (the best one), not all thresholds at once.
        // ET-based ranks (IsEnrollmentBased=true): Silver/Gold/Platinum — compare against EnrollmentPoints.
        // DT-based ranks (IsEnrollmentBased=false): Titanium+ — compare against DualTeamPoints.
        // Types sorted by Amount descending so FirstOrDefault always picks the highest paying tier.
        var residualTypesSorted = residualTypes.OrderByDescending(t => t.ActiveAmount ?? 0).ToList();
        var userId = _currentUser.UserId;

        // Build DailyResidualEarning rows (new table — replaces direct CommissionEarning writes).
        // Four new snapshot fields are populated from the in-memory data already loaded.
        var earnings = (
            from s in stats
            let qualifyingType = residualTypesSorted
                .FirstOrDefault(ct2 => ct2.IsEnrollmentBased
                    ? s.EnrollmentPoints >= ct2.TeamPoints
                    : s.DualTeamPoints >= ct2.TeamPoints)
            where qualifyingType != null
            let amount = qualifyingType.ActiveAmount
                         ?? Math.Round((decimal)s.DualTeamPoints * qualifyingType.Percentage / 100, 2)
            where amount > 0
            select new DailyResidualEarning
            {
                BeneficiaryMemberId          = s.MemberId,
                Amount                       = amount,
                EarnedDate                   = now,
                Status                       = CommissionEarningStatus.Pending,
                Notes                        = $"Daily residual — period {periodDate:yyyy-MM-dd}",
                CreatedBy                    = userId,
                CreationDate                 = now,

                // ── Snapshot fields ──────────────────────────────────────────────────
                CurrentRankId                = memberCurrentRankId.GetValueOrDefault(s.MemberId),

                // For ET-based tier winners, EligibleDualTeamPoints = 0 (DT wasn't the qualifying axis).
                // For DT-based tier winners, EligibleEnrollmentTeamPoints = 0 (ET wasn't the qualifying axis).
                EligibleDualTeamPoints       = qualifyingType.IsEnrollmentBased ? 0 : s.DualTeamPoints,
                EligibleEnrollmentTeamPoints = qualifyingType.IsEnrollmentBased ? s.EnrollmentPoints : 0,

                PersonalPoints               = s.PersonalPoints
            }
        ).ToList();

        if (earnings.Count > 0)
        {
            await _db.DailyResidualEarnings.AddRangeAsync(earnings, ct);
            await _db.SaveChangesAsync(ct);
        }

        return Result<CalculationResultResponse>.Success(new CalculationResultResponse
        {
            CommissionType = "DailyResidual",
            RecordsCreated = earnings.Count,
            TotalAmountCalculated = earnings.Sum(e => e.Amount),
            PeriodDate = periodDate,
            SkippedReasons = new()
        });
    }
}

