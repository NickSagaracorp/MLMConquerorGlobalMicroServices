using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetDualTreeNode;

public class GetDualTreeNodeHandler : IRequestHandler<GetDualTreeNodeQuery, Result<DualTreeNodeDto>>
{
    private static readonly Dictionary<int, int> LevelPoints = new() { [2] = 1, [3] = 6, [4] = 6 };

    private readonly AppDbContext _db;

    public GetDualTreeNodeHandler(AppDbContext db) => _db = db;

    public async Task<Result<DualTreeNodeDto>> Handle(GetDualTreeNodeQuery request, CancellationToken ct)
    {
        var memberId = request.NodeMemberId;

        // The requested node itself
        var node = await _db.DualTeamTree
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == memberId, ct);

        // Immediate children
        var children = await _db.DualTeamTree
            .AsNoTracking()
            .Where(d => d.ParentMemberId == memberId)
            .ToListAsync(ct);

        // Grand-children (to determine HasLeft/HasRight for each child)
        var childIds       = children.Select(c => c.MemberId).ToList();
        var grandchildren  = await _db.DualTeamTree
            .AsNoTracking()
            .Where(d => childIds.Contains(d.ParentMemberId))
            .ToListAsync(ct);

        // All member IDs we need profiles for
        var allIds = new List<string> { memberId };
        allIds.AddRange(childIds);

        var profiles = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => allIds.Contains(m.MemberId))
            .ToDictionaryAsync(m => m.MemberId, ct);

        // Active subscriptions for status + points
        var activeSubs = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Where(s => allIds.Contains(s.MemberId)
                     && s.SubscriptionStatus == MembershipStatus.Active
                     && !s.IsDeleted)
            .Select(s => new { s.MemberId, s.MembershipLevelId, s.CreationDate })
            .ToListAsync(ct);

        var subByMember = activeSubs
            .GroupBy(s => s.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CreationDate).First().MembershipLevelId);

        string ResolveStatus(string mid)
        {
            if (!profiles.TryGetValue(mid, out var p)) return "unqualified";
            return p.Status.ToString().ToLower() switch
            {
                "active"    => subByMember.ContainsKey(mid) ? "qualified" : "unqualified",
                "inactive"  => "inactive",
                "suspended" => "suspended",
                "cancelled" => "cancelled",
                _           => "unqualified"
            };
        }

        int ResolvePoints(string mid) =>
            subByMember.TryGetValue(mid, out var lid) && LevelPoints.TryGetValue(lid, out var pts) ? pts : 0;

        string ResolveName(string mid) =>
            profiles.TryGetValue(mid, out var p)
                ? $"{p.FirstName} {p.LastName}".Trim()
                : mid;

        // Build left / right child DTOs
        var leftDual  = children.FirstOrDefault(c => c.Side == Domain.Enums.TreeSide.Left);
        var rightDual = children.FirstOrDefault(c => c.Side == Domain.Enums.TreeSide.Right);

        DualTreeChildDto? BuildChild(Domain.Entities.Tree.DualTeamEntity? dual)
        {
            if (dual is null) return null;
            var gc = grandchildren.Where(g => g.ParentMemberId == dual.MemberId).ToList();
            return new DualTreeChildDto
            {
                MemberId   = dual.MemberId,
                FullName   = ResolveName(dual.MemberId),
                StatusCode = ResolveStatus(dual.MemberId),
                Points     = ResolvePoints(dual.MemberId),
                HasLeft    = gc.Any(g => g.Side == Domain.Enums.TreeSide.Left),
                HasRight   = gc.Any(g => g.Side == Domain.Enums.TreeSide.Right)
            };
        }

        var dto = new DualTreeNodeDto
        {
            MemberId   = memberId,
            FullName   = ResolveName(memberId),
            StatusCode = ResolveStatus(memberId),
            Points     = ResolvePoints(memberId),
            LeftChild  = BuildChild(leftDual),
            RightChild = BuildChild(rightDual)
        };

        return Result<DualTreeNodeDto>.Success(dto);
    }
}
