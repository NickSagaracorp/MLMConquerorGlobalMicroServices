using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCarBonusStats;

public class GetCarBonusStatsHandler : IRequestHandler<GetCarBonusStatsQuery, Result<CarBonusStatsDto>>
{
    // Comp plan: Elite=6 pts, Turbo=6 pts, VIP=1 pt per active subscription
    private static readonly Dictionary<int, int> LevelPoints = new() { [2] = 1, [3] = 6, [4] = 6 };

    private const int CarBonusTypeId = 85;

    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _dateTime;

    public GetCarBonusStatsHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
    }

    public async Task<Result<CarBonusStatsDto>> Handle(GetCarBonusStatsQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var today    = _dateTime.UtcNow;

        // Resolve Car Bonus threshold from CommissionType config
        var carBonusType = await _db.CommissionTypes
            .AsNoTracking()
            .Where(t => t.Id == CarBonusTypeId && t.IsActive)
            .FirstOrDefaultAsync(ct);

        int teamPointsTarget = carBonusType?.TeamPoints ?? 1000;

        // Get all active downline subscription levels (one row per member, latest subscription)
        var hierarchyFilter = $"/{memberId}/";

        var downlineMemberIds = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => g.HierarchyPath.Contains(hierarchyFilter) && g.MemberId != memberId)
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        int totalPoints = 0;
        if (downlineMemberIds.Count > 0)
        {
            var activeSubs = await _db.MembershipSubscriptions
                .AsNoTracking()
                .Where(s => downlineMemberIds.Contains(s.MemberId)
                         && s.SubscriptionStatus == MembershipStatus.Active
                         && !s.IsDeleted)
                .Select(s => new { s.MemberId, s.MembershipLevelId, s.CreationDate })
                .ToListAsync(ct);

            // Pick highest-level subscription per member and sum points
            totalPoints = activeSubs
                .GroupBy(s => s.MemberId)
                .Sum(g =>
                {
                    var levelId = g.OrderByDescending(s => s.CreationDate).First().MembershipLevelId;
                    return LevelPoints.TryGetValue(levelId, out var pts) ? pts : 0;
                });
        }

        var eligiblePoints  = totalPoints;
        var progressPercent = teamPointsTarget > 0
            ? (int)Math.Min(100, Math.Floor(eligiblePoints * 100.0 / teamPointsTarget))
            : 0;

        var dto = new CarBonusStatsDto
        {
            TotalPoints      = totalPoints,
            EligiblePoints   = eligiblePoints,
            ProgressPercent  = progressPercent,
            TeamPointsTarget = teamPointsTarget,
            MonthLabel       = today.ToString("MMMM yyyy")
        };

        return Result<CarBonusStatsDto>.Success(dto);
    }
}
