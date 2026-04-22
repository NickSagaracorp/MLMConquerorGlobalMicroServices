using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCarBonusAmbassadors;

public class GetCarBonusAmbassadorsHandler : IRequestHandler<GetCarBonusAmbassadorsQuery, Result<List<CarBonusAmbassadorDto>>>
{
    private static readonly Dictionary<int, int> LevelPoints = new() { [2] = 1, [3] = 6, [4] = 6 };

    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;

    public GetCarBonusAmbassadorsHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<Result<List<CarBonusAmbassadorDto>>> Handle(GetCarBonusAmbassadorsQuery request, CancellationToken ct)
    {
        var memberId        = _currentUser.MemberId;
        var hierarchyFilter = $"/{memberId}/";

        // Get ALL downline member IDs regardless of enrollment date
        var downlineMemberIds = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => g.HierarchyPath.Contains(hierarchyFilter) && g.MemberId != memberId)
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        if (downlineMemberIds.Count == 0)
            return Result<List<CarBonusAmbassadorDto>>.Success(new List<CarBonusAmbassadorDto>());

        // Get member profiles
        var profiles = await _db.MemberProfiles
            .AsNoTracking()
            .Where(mp => downlineMemberIds.Contains(mp.MemberId))
            .Select(mp => new { mp.MemberId, mp.FirstName, mp.LastName })
            .ToListAsync(ct);

        // Get active subscriptions
        var activeSubs = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Where(s => downlineMemberIds.Contains(s.MemberId)
                     && s.SubscriptionStatus == MembershipStatus.Active
                     && !s.IsDeleted)
            .Select(s => new { s.MemberId, s.MembershipLevelId, s.CreationDate })
            .ToListAsync(ct);

        var subsByMember = activeSubs
            .GroupBy(s => s.MemberId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(s => s.CreationDate).First().MembershipLevelId);

        var result = profiles
            .Select(mp =>
            {
                var levelId = subsByMember.TryGetValue(mp.MemberId, out var lid) ? lid : 0;
                var pts     = LevelPoints.TryGetValue(levelId, out var p) ? p : 0;
                return new CarBonusAmbassadorDto
                {
                    MemberId       = mp.MemberId,
                    AmbassadorName = $"{mp.FirstName} {mp.LastName}".Trim(),
                    TotalPoints    = pts,
                    EligiblePoints = pts
                };
            })
            .OrderByDescending(x => x.TotalPoints)
            .ThenBy(x => x.AmbassadorName)
            .ToList();

        return Result<List<CarBonusAmbassadorDto>>.Success(result);
    }
}
