using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <inheritdoc />
public class DualTreeNodeService : IDualTreeNodeService
{
    private readonly AppDbContext _db;

    public DualTreeNodeService(AppDbContext db) => _db = db;

    public async Task<DualTreeNodeView> GetNodeAsync(string rootMemberId, CancellationToken ct = default)
    {
        var rootDual = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == rootMemberId, ct);

        // Level 1 — direct children
        var children = await _db.DualTeamTree.AsNoTracking()
            .Where(d => d.ParentMemberId == rootMemberId)
            .ToListAsync(ct);

        var childIds = children.Select(c => c.MemberId).ToList();

        // Level 2 — grandchildren (full entities)
        var grandchildren = childIds.Count > 0
            ? await _db.DualTeamTree.AsNoTracking()
                .Where(d => childIds.Contains(d.ParentMemberId!))
                .ToListAsync(ct)
            : new List<DualTeamEntity>();

        var grandchildIds = grandchildren.Select(g => g.MemberId).ToList();

        // Level 3 — only the side flags, used to set HasLeft/HasRight on grandchildren
        var ggcSidesByParent = new Dictionary<string, HashSet<TreeSide>>();
        if (grandchildIds.Count > 0)
        {
            var ggc = await _db.DualTeamTree.AsNoTracking()
                .Where(d => grandchildIds.Contains(d.ParentMemberId!))
                .Select(d => new { d.ParentMemberId, d.Side })
                .ToListAsync(ct);
            ggcSidesByParent = ggc
                .GroupBy(x => x.ParentMemberId!)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Side).ToHashSet());
        }

        var allIds = new HashSet<string> { rootMemberId };
        allIds.UnionWith(childIds);
        allIds.UnionWith(grandchildIds);

        var profiles = await _db.MemberProfiles.AsNoTracking()
            .Where(m => allIds.Contains(m.MemberId))
            .ToDictionaryAsync(m => m.MemberId, ct);

        var activeMemberIds = (await _db.MembershipSubscriptions.AsNoTracking()
            .Where(s => allIds.Contains(s.MemberId)
                     && s.SubscriptionStatus == MembershipStatus.Active
                     && !s.IsDeleted)
            .Select(s => s.MemberId)
            .ToListAsync(ct))
            .ToHashSet();

        var personalMap = await _db.MemberStatistics.AsNoTracking()
            .Where(s => allIds.Contains(s.MemberId))
            .ToDictionaryAsync(s => s.MemberId, s => s.PersonalPoints, ct);

        var dualMap = new[] { rootDual }
            .Concat(children)
            .Concat(grandchildren)
            .Where(d => d is not null)
            .ToDictionary(d => d!.MemberId, d => d!);

        int Points(string mid) =>
            dualMap.TryGetValue(mid, out var d) ? (int)(d.LeftLegPoints + d.RightLegPoints) : 0;

        int Personal(string mid) =>
            personalMap.TryGetValue(mid, out var pp) ? pp : 0;

        string Status(string mid)
        {
            if (!profiles.TryGetValue(mid, out var p)) return "unqualified";
            return p.Status switch
            {
                MemberAccountStatus.Active    => activeMemberIds.Contains(mid) ? "qualified" : "unqualified",
                MemberAccountStatus.Inactive  => "inactive",
                MemberAccountStatus.Suspended => "suspended",
                _                             => "cancelled"
            };
        }

        string Name(string mid) =>
            profiles.TryGetValue(mid, out var p)
                ? $"{p.FirstName} {p.LastName}".Trim()
                : mid;

        var grandchildByParent = grandchildren
            .GroupBy(g => g.ParentMemberId!)
            .ToDictionary(g => g.Key, g => g.ToList());

        DualTreeGrandchildView BuildGrandchild(DualTeamEntity gc)
        {
            var ggcSides = ggcSidesByParent.GetValueOrDefault(gc.MemberId) ?? new HashSet<TreeSide>();
            return new DualTreeGrandchildView
            {
                MemberId       = gc.MemberId,
                FullName       = Name(gc.MemberId),
                StatusCode     = Status(gc.MemberId),
                Points         = Points(gc.MemberId),
                PersonalPoints = Personal(gc.MemberId),
                HasLeft        = ggcSides.Contains(TreeSide.Left),
                HasRight       = ggcSides.Contains(TreeSide.Right)
            };
        }

        DualTreeChildView? BuildChild(DualTeamEntity? child)
        {
            if (child is null) return null;
            var gcList  = grandchildByParent.GetValueOrDefault(child.MemberId) ?? new List<DualTeamEntity>();
            var leftGc  = gcList.FirstOrDefault(g => g.Side == TreeSide.Left);
            var rightGc = gcList.FirstOrDefault(g => g.Side == TreeSide.Right);
            return new DualTreeChildView
            {
                MemberId       = child.MemberId,
                FullName       = Name(child.MemberId),
                StatusCode     = Status(child.MemberId),
                Points         = Points(child.MemberId),
                PersonalPoints = Personal(child.MemberId),
                HasLeft        = leftGc  is not null,
                HasRight       = rightGc is not null,
                LeftChild      = leftGc  is not null ? BuildGrandchild(leftGc)  : null,
                RightChild     = rightGc is not null ? BuildGrandchild(rightGc) : null
            };
        }

        var leftDual  = children.FirstOrDefault(c => c.Side == TreeSide.Left);
        var rightDual = children.FirstOrDefault(c => c.Side == TreeSide.Right);

        return new DualTreeNodeView
        {
            MemberId       = rootMemberId,
            FullName       = Name(rootMemberId),
            StatusCode     = Status(rootMemberId),
            Points         = Points(rootMemberId),
            PersonalPoints = Personal(rootMemberId),
            LeftChild      = BuildChild(leftDual),
            RightChild     = BuildChild(rightDual)
        };
    }
}
