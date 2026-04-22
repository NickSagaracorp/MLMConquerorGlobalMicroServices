using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateCarBonus;

public class CalculateCarBonusHandler : IRequestHandler<CalculateCarBonusCommand, Result<CalculationResultResponse>>
{
    // Comp plan: Elite=6 pts, Turbo=6 pts, VIP=1 pt per active subscription
    private static readonly Dictionary<int, int> LevelPoints = new() { [2] = 1, [3] = 6, [4] = 6 };

    private const int CarBonusTypeId     = 85;
    private const int CarBonusCategoryId = 4;

    private readonly AppDbContext        _db;
    private readonly IDateTimeProvider   _dateTime;
    private readonly ICurrentUserService _currentUser;

    public CalculateCarBonusHandler(AppDbContext db, IDateTimeProvider dateTime, ICurrentUserService currentUser)
    {
        _db          = db;
        _dateTime    = dateTime;
        _currentUser = currentUser;
    }

    public async Task<Result<CalculationResultResponse>> Handle(
        CalculateCarBonusCommand command, CancellationToken ct)
    {
        var now = _dateTime.Now;

        // Resolve the period: first day of the target month
        var periodDate = command.PeriodDate.HasValue
            ? new DateTime(command.PeriodDate.Value.Year, command.PeriodDate.Value.Month, 1)
            : new DateTime(now.Year, now.Month, 1);
        var monthEnd = periodDate.AddMonths(1);

        // Load Car Bonus commission type
        var carBonusType = await _db.CommissionTypes
            .AsNoTracking()
            .Where(t => t.Id == CarBonusTypeId && t.IsActive && t.CommissionCategoryId == CarBonusCategoryId)
            .FirstOrDefaultAsync(ct);

        if (carBonusType is null)
            return Result<CalculationResultResponse>.Failure(
                "NO_CAR_BONUS_TYPE", "No active Car Bonus commission type configured (ID 85).");

        // Idempotency: skip if already calculated for this month
        var alreadyRan = await _db.CommissionEarnings
            .AnyAsync(e => e.CommissionTypeId == CarBonusTypeId
                        && e.PeriodDate.HasValue
                        && e.PeriodDate.Value >= periodDate
                        && e.PeriodDate.Value < monthEnd, ct);

        if (alreadyRan)
            return Result<CalculationResultResponse>.Failure(
                "ALREADY_CALCULATED",
                $"Car Bonus for {periodDate:yyyy-MM} was already calculated.");

        // Resolve all active ambassadors (membership level 3 or 4 = Elite/Turbo)
        var ambassadors = await (
            from sub in _db.MembershipSubscriptions.AsNoTracking()
            where sub.SubscriptionStatus == MembershipStatus.Active
               && !sub.IsDeleted
               && (sub.MembershipLevelId == 3 || sub.MembershipLevelId == 4)
            select sub.MemberId
        ).Distinct().ToListAsync(ct);

        if (ambassadors.Count == 0)
            return Result<CalculationResultResponse>.Success(new CalculationResultResponse
            {
                CommissionType        = "CarBonus",
                RecordsCreated        = 0,
                TotalAmountCalculated = 0,
                PeriodDate            = periodDate,
                SkippedReasons        = ["No active Elite/Turbo ambassadors found."]
            });

        // Load all genealogy paths once
        var ambassadorSet = ambassadors.ToHashSet();

        var allPaths = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => ambassadorSet.Contains(g.MemberId))
            .Select(g => new { g.MemberId, g.HierarchyPath })
            .ToListAsync(ct);

        var pathByMember = allPaths.ToDictionary(x => x.MemberId, x => x.HierarchyPath);

        // Load all active downline subscriptions (for team-point calculation)
        var allActiveDownline = await (
            from sub in _db.MembershipSubscriptions.AsNoTracking()
            join g   in _db.GenealogyTree.AsNoTracking() on sub.MemberId equals g.MemberId
            where sub.SubscriptionStatus == MembershipStatus.Active
               && !sub.IsDeleted
            select new { sub.MemberId, sub.MembershipLevelId, g.HierarchyPath }
        ).ToListAsync(ct);

        var skipped   = new List<string>();
        var earnings  = new List<CommissionEarning>();
        var payDate   = new DateTime(periodDate.AddMonths(1).Year, periodDate.AddMonths(1).Month, 15);

        foreach (var ambassadorId in ambassadors)
        {
            // Verify ambassador's own personal points
            var ownSub = await _db.MembershipSubscriptions
                .AsNoTracking()
                .Where(s => s.MemberId == ambassadorId
                         && s.SubscriptionStatus == MembershipStatus.Active
                         && !s.IsDeleted)
                .OrderByDescending(s => s.CreationDate)
                .FirstOrDefaultAsync(ct);

            var personalPoints = ownSub is not null
                && LevelPoints.TryGetValue(ownSub.MembershipLevelId, out var pp) ? pp : 0;

            if (personalPoints < carBonusType.PersonalPoints)
            {
                skipped.Add($"{ambassadorId}: personal points {personalPoints} < required {carBonusType.PersonalPoints}.");
                continue;
            }

            // Calculate team enrollment points from active downline
            var hierarchyFilter = $"/{ambassadorId}/";
            var teamPoints = allActiveDownline
                .Where(x => x.HierarchyPath.Contains(hierarchyFilter) && x.MemberId != ambassadorId)
                .GroupBy(x => x.MemberId)
                .Sum(g =>
                {
                    var latest = g.OrderByDescending(x => x.MemberId).First();
                    return LevelPoints.TryGetValue(latest.MembershipLevelId, out var pts) ? pts : 0;
                });

            if (teamPoints < carBonusType.TeamPoints)
            {
                skipped.Add($"{ambassadorId}: team points {teamPoints} < required {carBonusType.TeamPoints}.");
                continue;
            }

            var amount = carBonusType.ActiveAmount ?? 500m;
            if (amount <= 0)
            {
                skipped.Add($"{ambassadorId}: Car Bonus amount is 0.");
                continue;
            }

            earnings.Add(new CommissionEarning
            {
                BeneficiaryMemberId = ambassadorId,
                CommissionTypeId    = CarBonusTypeId,
                Amount              = amount,
                Status              = CommissionEarningStatus.Pending,
                EarnedDate          = now,
                PaymentDate         = payDate,
                PeriodDate          = periodDate,
                Notes               = $"car-bonus-{periodDate:yyyy-MM}",
                CreatedBy           = _currentUser.UserId,
                CreationDate        = now,
                LastUpdateDate      = now
            });
        }

        if (earnings.Count > 0)
        {
            await _db.CommissionEarnings.AddRangeAsync(earnings, ct);
            await _db.SaveChangesAsync(ct);
        }

        return Result<CalculationResultResponse>.Success(new CalculationResultResponse
        {
            CommissionType        = "CarBonus",
            RecordsCreated        = earnings.Count,
            TotalAmountCalculated = earnings.Sum(e => e.Amount),
            PeriodDate            = periodDate,
            SkippedReasons        = skipped
        });
    }
}
