using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateDailyResidual;

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
        var residualTypeIds = residualTypes.Select(t => t.Id).ToList();
        var alreadyRan = await _db.CommissionEarnings
            .AnyAsync(e => residualTypeIds.Contains(e.CommissionTypeId)
                        && e.PeriodDate.HasValue && e.PeriodDate.Value.Date == periodDate, ct);

        if (alreadyRan)
            return Result<CalculationResultResponse>.Failure(
                "ALREADY_CALCULATED",
                $"Daily residual for period {periodDate:yyyy-MM-dd} was already calculated.");

        // Single bulk query: stats for all active ambassadors.
        // Include both DualTeamPoints (Titanium+) and EnrollmentPoints (Silver/Gold/Platinum).
        var stats = await (
            from s in _db.MemberStatistics.AsNoTracking()
            join m in _db.MemberProfiles.AsNoTracking()
                on s.MemberId equals m.MemberId
            where m.MemberType == Domain.Enums.MemberType.Ambassador
               && m.Status == Domain.Entities.Member.MemberAccountStatus.Active
               && (s.DualTeamPoints > 0 || s.EnrollmentPoints > 0)
            select new { s.MemberId, s.DualTeamPoints, s.EnrollmentPoints }
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

        // For each ambassador: find the HIGHEST qualifying DTR tier and pay that fixed daily amount.
        // An ambassador qualifies at exactly one tier per day (the best one), not all thresholds at once.
        // ET-based ranks (IsEnrollmentBased=true): Silver/Gold/Platinum — compare against EnrollmentPoints.
        // DT-based ranks (IsEnrollmentBased=false): Titanium+ — compare against DualTeamPoints.
        // Types sorted by FixedAmount descending so FirstOrDefault always picks the highest paying tier.
        var residualTypesSorted = residualTypes.OrderByDescending(t => t.FixedAmount ?? 0).ToList();
        var userId = _currentUser.UserId;

        var earnings = (
            from s in stats
            let qualifyingType = residualTypesSorted
                .FirstOrDefault(ct2 => ct2.IsEnrollmentBased
                    ? s.EnrollmentPoints >= ct2.TeamPoints
                    : s.DualTeamPoints >= ct2.TeamPoints)
            where qualifyingType != null
            let amount = qualifyingType.FixedAmount
                         ?? Math.Round((decimal)s.DualTeamPoints * qualifyingType.Percentage / 100, 2)
            where amount > 0
            select new CommissionEarning
            {
                BeneficiaryMemberId = s.MemberId,
                CommissionTypeId = qualifyingType.Id,
                Amount = amount,
                Status = CommissionEarningStatus.Pending,
                EarnedDate = now,
                PaymentDate = now.AddDays(qualifyingType.PaymentDelayDays),
                PeriodDate = periodDate,
                CreatedBy = userId,
                CreationDate = now,
                LastUpdateDate = now
            }
        ).ToList();

        if (earnings.Count > 0)
        {
            await _db.CommissionEarnings.AddRangeAsync(earnings, ct);
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
