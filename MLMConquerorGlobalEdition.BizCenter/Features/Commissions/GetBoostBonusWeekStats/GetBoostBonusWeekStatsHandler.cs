using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;
using IDateTimeProvider    = MLMConquerorGlobalEdition.BizCenter.Services.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetBoostBonusWeekStats;

public class GetBoostBonusWeekStatsHandler : IRequestHandler<GetBoostBonusWeekStatsQuery, Result<BoostBonusWeekStatsDto>>
{
    private const int BoostBonusCategoryId = 4;

    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _dateTime;

    public GetBoostBonusWeekStatsHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
    }

    public async Task<Result<BoostBonusWeekStatsDto>> Handle(GetBoostBonusWeekStatsQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var today    = _dateTime.UtcNow;

        var dayOfWeek  = (int)today.DayOfWeek;
        var daysToMon  = dayOfWeek == 0 ? -6 : -(dayOfWeek - 1);
        var weekStart  = today.AddDays(daysToMon).Date;
        var weekEnd    = weekStart.AddDays(7);

        // Resolve boost thresholds from CommissionTypes
        var boostTypes = await _db.CommissionTypes
            .AsNoTracking()
            .Where(t => t.IsActive && !t.IsPaidOnSignup && !t.ResidualBased
                     && !t.IsSponsorBonus && t.CommissionCategoryId == BoostBonusCategoryId)
            .OrderBy(t => t.LifeTimeRank)
            .ToListAsync(ct);

        var goldType     = boostTypes.FirstOrDefault(t => t.Name.Contains("Gold",     StringComparison.OrdinalIgnoreCase));
        var platinumType = boostTypes.FirstOrDefault(t => t.Name.Contains("Platinum", StringComparison.OrdinalIgnoreCase));

        int goldTarget     = goldType?.NewMembers     ?? 6;
        int platinumTarget = platinumType?.NewMembers ?? 12;

        // New Elite/Turbo Active enrollments in downline this week
        int[] eligibleLevelIds = [3, 4];
        var hierarchyFilter    = $"/{memberId}/";

        var downlineEnrollments = await (
            from mp  in _db.MemberProfiles.AsNoTracking()
            join g   in _db.GenealogyTree.AsNoTracking()           on mp.MemberId equals g.MemberId
            join sub in _db.MembershipSubscriptions.AsNoTracking() on mp.MemberId equals sub.MemberId
            where mp.EnrollDate >= weekStart
               && mp.EnrollDate <  weekEnd
               && sub.SubscriptionStatus == MembershipStatus.Active
               && eligibleLevelIds.Contains(sub.MembershipLevelId)
               && g.HierarchyPath.Contains(hierarchyFilter)
            select new { mp.MemberId, g.HierarchyPath }
        ).Distinct().ToListAsync(ct);

        static string GetLeg(string path, string filter)
        {
            var idx = path.IndexOf(filter, StringComparison.Ordinal);
            if (idx < 0) return string.Empty;
            var after = path[(idx + filter.Length)..];
            var slash = after.IndexOf('/');
            return slash > 0 ? after[..slash] : after.TrimEnd('/');
        }

        int CappedCount(int threshold)
        {
            if (threshold == 0) return downlineEnrollments.Count;
            var capPerLeg = (int)Math.Floor(threshold * 0.5);
            return downlineEnrollments
                .Select(e => GetLeg(e.HierarchyPath, hierarchyFilter))
                .GroupBy(leg => leg)
                .Sum(g => Math.Min(g.Count(), capPerLeg));
        }

        // Qualification points per enrolled member from completed orders
        var enrolledIds = downlineEnrollments.Select(e => e.MemberId).Distinct().ToList();

        Dictionary<string, int> pointsPerMember;
        if (enrolledIds.Count > 0)
        {
            var memberPointsData = await (
                from o  in _db.Orders.AsNoTracking()
                join od in _db.OrderDetails.AsNoTracking() on o.Id equals od.OrderId
                join p  in _db.Products.AsNoTracking()     on od.ProductId equals p.Id
                where enrolledIds.Contains(o.MemberId)
                   && o.Status == OrderStatus.Completed
                select new { o.MemberId, Points = od.Quantity * p.QualificationPoins }
            ).ToListAsync(ct);

            pointsPerMember = memberPointsData
                .GroupBy(x => x.MemberId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Points));
        }
        else
        {
            pointsPerMember = new Dictionary<string, int>();
        }

        var minProductPoints = enrolledIds.Count > 0
            ? await _db.Products.AsNoTracking()
                .Where(p => p.IsActive && !p.IsDeleted && p.QualificationPoins >= 6)
                .MinAsync(p => (int?)p.QualificationPoins, ct) ?? 6
            : 6;

        int CappedPoints(int threshold)
        {
            if (threshold == 0)
                return downlineEnrollments.DistinctBy(e => e.MemberId)
                    .Sum(e => pointsPerMember.TryGetValue(e.MemberId, out var pts) ? pts : 0);
            var capPerLeg = (int)Math.Floor(threshold * 0.5);
            return downlineEnrollments
                .GroupBy(e => GetLeg(e.HierarchyPath, hierarchyFilter))
                .Sum(g => g.DistinctBy(e => e.MemberId)
                           .Take(capPerLeg)
                           .Sum(e => pointsPerMember.TryGetValue(e.MemberId, out var p2) ? p2 : 0));
        }

        var dto = new BoostBonusWeekStatsDto
        {
            WeekLabel            = $"Week of {weekStart:MMM d} - {weekStart.AddDays(6):MMM d}",
            GoldCount            = CappedCount(goldTarget),
            PlatinumCount        = CappedCount(platinumTarget),
            GoldTarget           = goldTarget,
            PlatinumTarget       = platinumTarget,
            GoldPoints           = CappedPoints(goldTarget),
            GoldPointsTarget     = goldTarget * minProductPoints,
            PlatinumPoints       = CappedPoints(platinumTarget),
            PlatinumPointsTarget = platinumTarget * minProductPoints
        };

        return Result<BoostBonusWeekStatsDto>.Success(dto);
    }
}
