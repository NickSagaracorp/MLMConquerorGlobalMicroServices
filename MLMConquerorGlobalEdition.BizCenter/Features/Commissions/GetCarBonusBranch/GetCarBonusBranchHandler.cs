using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCarBonusBranch;

public class GetCarBonusBranchHandler : IRequestHandler<GetCarBonusBranchQuery, Result<CarBonusBranchDto>>
{
    private static readonly Dictionary<int, int> LevelPoints = new() { [2] = 1, [3] = 6, [4] = 6 };

    private readonly AppDbContext _db;

    public GetCarBonusBranchHandler(AppDbContext db) => _db = db;

    public async Task<Result<CarBonusBranchDto>> Handle(GetCarBonusBranchQuery request, CancellationToken ct)
    {
        var branchMemberId  = request.BranchMemberId;
        var hierarchyFilter = $"/{branchMemberId}/";

        // Include the branch ambassador themselves + all their downline
        var memberIds = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => g.MemberId == branchMemberId
                     || g.HierarchyPath.Contains(hierarchyFilter))
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        if (memberIds.Count == 0)
            return Result<CarBonusBranchDto>.Success(new CarBonusBranchDto());

        // Active subscriptions
        var activeSubs = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Where(s => memberIds.Contains(s.MemberId)
                     && s.SubscriptionStatus == MembershipStatus.Active
                     && !s.IsDeleted)
            .Select(s => new
            {
                s.MemberId,
                s.MembershipLevelId,
                s.EndDate,
                s.LastOrderId,
                s.CreationDate
            })
            .ToListAsync(ct);

        var subsByMember = activeSubs
            .GroupBy(s => s.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CreationDate).First());

        // Membership level names
        var levelIds = subsByMember.Values.Select(s => s.MembershipLevelId).Distinct().ToList();
        var levels   = await _db.MembershipLevels
            .AsNoTracking()
            .Where(l => levelIds.Contains(l.Id))
            .Select(l => new { l.Id, l.Name })
            .ToDictionaryAsync(l => l.Id, l => l.Name, ct);

        // Order numbers via LastOrderId
        var orderIds = subsByMember.Values
            .Where(s => s.LastOrderId != null)
            .Select(s => s.LastOrderId!)
            .Distinct()
            .ToList();

        var orderNos = await _db.Orders
            .AsNoTracking()
            .Where(o => orderIds.Contains(o.Id))
            .Select(o => new { o.Id, o.OrderNo })
            .ToDictionaryAsync(o => o.Id, o => o.OrderNo ?? string.Empty, ct);

        // Member profiles
        var profiles = await _db.MemberProfiles
            .AsNoTracking()
            .Where(mp => memberIds.Contains(mp.MemberId))
            .Select(mp => new { mp.MemberId, mp.FirstName, mp.LastName })
            .ToListAsync(ct);

        var members = profiles
            .Select(mp =>
            {
                var sub        = subsByMember.TryGetValue(mp.MemberId, out var s) ? s : null;
                var levelId    = sub?.MembershipLevelId ?? 0;
                var pts        = LevelPoints.TryGetValue(levelId, out var p) ? p : 0;
                var levelName  = levelId > 0 && levels.TryGetValue(levelId, out var ln) ? ln : "—";
                var orderNo    = sub?.LastOrderId != null && orderNos.TryGetValue(sub.LastOrderId, out var on) ? on : "—";
                var expDate    = sub?.EndDate;

                return new CarBonusBranchMemberDto
                {
                    OrderNo         = orderNo,
                    FullName        = $"{mp.FirstName} {mp.LastName}".Trim(),
                    MembershipLevel = levelName,
                    ExpirationDate  = expDate,
                    Points          = pts
                };
            })
            .OrderByDescending(x => x.Points)
            .ThenBy(x => x.FullName)
            .ToList();

        return Result<CarBonusBranchDto>.Success(new CarBonusBranchDto { Members = members });
    }
}
