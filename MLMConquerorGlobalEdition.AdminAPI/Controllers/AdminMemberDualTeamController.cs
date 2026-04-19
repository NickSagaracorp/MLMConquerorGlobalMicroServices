using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Dual Team data for a specific member — used by Admin Member Profile Dual Team tab.
/// Routes: /api/v1/admin/members/{memberId}/team/*
/// </summary>
[ApiController]
[Route("api/v1/admin/members/{memberId}/team")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminMemberDualTeamController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminMemberDualTeamController(AppDbContext db) => _db = db;

    // ─── My Dual Team Members ────────────────────────────────────────────────
    [HttpGet("members")]
    public async Task<IActionResult> GetMembers(
        string memberId,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 20,
        [FromQuery] string? search   = null,
        CancellationToken ct = default)
    {
        // 1. Get all dual team members under this member via HierarchyPath
        var rootNode = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == memberId, ct);

        if (rootNode is null)
            return Ok(ApiResponse<PagedResult<DualTeamMemberDto>>.Ok(new PagedResult<DualTeamMemberDto>()));

        var pathPrefix = rootNode.HierarchyPath;

        // 2. All nodes in subtree (excluding root itself)
        var subtreeNodes = await _db.DualTeamTree.AsNoTracking()
            .Where(d => d.HierarchyPath.StartsWith(pathPrefix) && d.MemberId != memberId)
            .Select(d => new { d.MemberId, d.ParentMemberId, d.Side, d.HierarchyPath, d.LeftLegPoints, d.RightLegPoints })
            .ToListAsync(ct);

        if (!subtreeNodes.Any())
            return Ok(ApiResponse<PagedResult<DualTeamMemberDto>>.Ok(new PagedResult<DualTeamMemberDto>()));

        var subtreeIds = subtreeNodes.Select(n => n.MemberId).ToHashSet();

        // Compute level from hierarchy depth
        var rootDepth = pathPrefix.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;
        var levelMap  = subtreeNodes.ToDictionary(
            n => n.MemberId,
            n => n.HierarchyPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length - rootDepth);

        var nodeMap = subtreeNodes.ToDictionary(n => n.MemberId);

        // 3. Member profiles
        var profileQuery = _db.MemberProfiles.AsNoTracking()
            .Where(p => subtreeIds.Contains(p.MemberId));

        if (!string.IsNullOrWhiteSpace(search))
            profileQuery = profileQuery.Where(p =>
                p.FirstName.Contains(search) || p.LastName.Contains(search) || p.MemberId.Contains(search));

        var totalCount = await profileQuery.CountAsync(ct);

        var profiles = await profileQuery
            .OrderBy(p => p.CreationDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.MemberId,
                p.FirstName,
                p.LastName,
                p.Country,
                p.SponsorMemberId,
                p.Status,
                p.CreationDate
            })
            .ToListAsync(ct);

        var pageIds = profiles.Select(p => p.MemberId).ToHashSet();

        // 4. Stats
        var stats = await _db.MemberStatistics.AsNoTracking()
            .Where(s => pageIds.Contains(s.MemberId))
            .ToDictionaryAsync(s => s.MemberId, ct);

        // 5. Subscriptions (latest per member) — load all then group in memory
        var allSubs = await _db.MembershipSubscriptions.AsNoTracking()
            .Where(s => pageIds.Contains(s.MemberId))
            .OrderByDescending(s => s.CreationDate)
            .ToListAsync(ct);
        var subMap = allSubs
            .GroupBy(s => s.MemberId)
            .ToDictionary(g => g.Key, g => g.First());

        // 6. Membership levels
        var levelIds = allSubs.Select(s => s.MembershipLevelId).Distinct().ToList();
        var levels   = await _db.MembershipLevels.AsNoTracking()
            .Where(l => levelIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Name, ct);

        // 7. Ranks (latest per member) — load all then group in memory
        var allRankHistories = await _db.MemberRankHistories.AsNoTracking()
            .Where(r => pageIds.Contains(r.MemberId))
            .Include(r => r.RankDefinition)
            .OrderByDescending(r => r.AchievedAt)
            .ToListAsync(ct);
        var rankMap = allRankHistories
            .GroupBy(r => r.MemberId)
            .ToDictionary(g => g.Key, g => g.First());

        // 8. All rank definitions (for next rank calc)
        var allRanks = await _db.RankDefinitions.AsNoTracking()
            .Include(r => r.Requirements)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        // 9. Sponsor names
        var sponsorIds = profiles.Where(p => p.SponsorMemberId != null)
            .Select(p => p.SponsorMemberId!).Distinct().ToList();
        var sponsorNames = await _db.MemberProfiles.AsNoTracking()
            .Where(p => sponsorIds.Contains(p.MemberId))
            .ToDictionaryAsync(p => p.MemberId, p => $"{p.FirstName} {p.LastName}", ct);

        // 10. Dual upline names
        var uplineIds = profiles.Select(p => p.MemberId)
            .Where(id => nodeMap.ContainsKey(id) && nodeMap[id].ParentMemberId != null)
            .Select(id => nodeMap[id].ParentMemberId!)
            .Distinct().ToList();
        var uplineNames = await _db.MemberProfiles.AsNoTracking()
            .Where(p => uplineIds.Contains(p.MemberId))
            .ToDictionaryAsync(p => p.MemberId, p => $"{p.FirstName} {p.LastName}", ct);

        // 11. Build DTOs
        var items = profiles.Select(p =>
        {
            stats.TryGetValue(p.MemberId, out var stat);
            subMap.TryGetValue(p.MemberId, out var sub);
            rankMap.TryGetValue(p.MemberId, out var rank);
            nodeMap.TryGetValue(p.MemberId, out var node);

            var currentRank      = rank?.RankDefinition;
            var currentSortOrder = currentRank?.SortOrder ?? 0;
            var nextRank  = allRanks.FirstOrDefault(r => r.SortOrder > currentSortOrder);
            var nextReq   = nextRank?.Requirements.FirstOrDefault();
            var dualPts   = stat?.DualTeamPoints ?? 0;
            var nextPct   = nextReq?.TeamPoints > 0
                ? Math.Min(100, (int)(dualPts * 100.0 / nextReq.TeamPoints))
                : 100;

            var bizStatus = p.Status switch
            {
                MemberAccountStatus.Active    => "Active",
                MemberAccountStatus.Inactive  => "Inactive",
                MemberAccountStatus.Suspended => "Suspended",
                MemberAccountStatus.Terminated=> "Terminated",
                _                             => "Pending"
            };

            var memStatus = sub?.SubscriptionStatus.ToString() ?? "Unknown";
            var qualified = p.Status == MemberAccountStatus.Active
                && (stat?.PersonalPoints ?? 0) >= 1
                    ? "Qualified" : "Unqualified";

            var leg = node != null
                ? (node.Side == TreeSide.Left ? "Left" : "Right")
                : "—";

            uplineNames.TryGetValue(node?.ParentMemberId ?? "", out var uplineName);
            sponsorNames.TryGetValue(p.SponsorMemberId ?? "", out var sponsorName);

            return new DualTeamMemberDto
            {
                MemberId            = p.MemberId,
                FullName            = $"{p.FirstName} {p.LastName}",
                Level               = levelMap.TryGetValue(p.MemberId, out var lv) ? lv : 0,
                Leg                 = leg,
                Country             = p.Country,
                SponsorName         = sponsorName ?? "—",
                DualTeamUplineName  = uplineName ?? "—",
                BizCenterStatus     = bizStatus,
                MembershipStatus    = memStatus,
                QualifiedStatus     = qualified,
                MembershipName      = sub != null && levels.TryGetValue(sub.MembershipLevelId, out var lvName) ? lvName : "—",
                RankName            = currentRank?.Name ?? "—",
                RankDate            = rank?.AchievedAt,
                LifetimeRankName    = currentRank?.Name ?? "—",
                NextRankPercent     = nextPct,
                QualificationPoints = stat?.PersonalPoints ?? 0,
                EnrollmentTeamPoints= stat?.EnrollmentPoints ?? 0,
                LeftTeamPoints      = (int)(node?.LeftLegPoints ?? 0),
                RightTeamPoints     = (int)(node?.RightLegPoints ?? 0),
                JoinDate            = p.CreationDate
            };
        }).ToList();

        var result = new PagedResult<DualTeamMemberDto>
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize
        };

        return Ok(ApiResponse<PagedResult<DualTeamMemberDto>>.Ok(result));
    }

    // ─── Binary Tree: Node ───────────────────────────────────────────────────
    [HttpGet("dual-tree/node/{nodeId}")]
    public async Task<IActionResult> GetTreeNode(
        string memberId,
        string nodeId,
        CancellationToken ct = default)
    {
        var node = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == nodeId, ct);

        if (node is null)
            return NotFound(ApiResponse<object>.Fail("NODE_NOT_FOUND", "Node not found."));

        var profile = await _db.MemberProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.MemberId == nodeId, ct);

        var stat = await _db.MemberStatistics.AsNoTracking()
            .FirstOrDefaultAsync(s => s.MemberId == nodeId, ct);

        var statusCode = profile?.Status switch
        {
            MemberAccountStatus.Active    => "Q",
            MemberAccountStatus.Inactive  => "U",
            MemberAccountStatus.Suspended => "U",
            _                             => "C"
        };

        // Direct children
        var children = await _db.DualTeamTree.AsNoTracking()
            .Where(d => d.ParentMemberId == nodeId)
            .ToListAsync(ct);

        var childIds = children.Select(c => c.MemberId).ToList();
        var childProfiles = await _db.MemberProfiles.AsNoTracking()
            .Where(p => childIds.Contains(p.MemberId))
            .ToDictionaryAsync(p => p.MemberId, ct);
        var childStats = await _db.MemberStatistics.AsNoTracking()
            .Where(s => childIds.Contains(s.MemberId))
            .ToDictionaryAsync(s => s.MemberId, ct);

        // Grandchildren (to determine HasLeft / HasRight for children)
        var grandchildren = await _db.DualTeamTree.AsNoTracking()
            .Where(d => childIds.Contains(d.ParentMemberId!))
            .Select(d => new { d.ParentMemberId, d.Side })
            .ToListAsync(ct);
        var grandchildMap = grandchildren
            .GroupBy(g => g.ParentMemberId!)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Side).ToHashSet());

        DualChildDto? MapChild(string childId)
        {
            if (!childProfiles.TryGetValue(childId, out var cp)) return null;
            var cs = childStats.GetValueOrDefault(childId);
            var sc = cp.Status switch
            {
                MemberAccountStatus.Active    => "Q",
                MemberAccountStatus.Inactive  => "U",
                MemberAccountStatus.Suspended => "U",
                _                             => "C"
            };
            var gc = grandchildMap.GetValueOrDefault(childId) ?? new HashSet<TreeSide>();
            return new DualChildDto
            {
                MemberId  = childId,
                FullName  = $"{cp.FirstName} {cp.LastName}",
                StatusCode= sc,
                Points    = cs?.DualTeamPoints ?? 0,
                HasLeft   = gc.Contains(TreeSide.Left),
                HasRight  = gc.Contains(TreeSide.Right)
            };
        }

        var leftChild  = children.FirstOrDefault(c => c.Side == TreeSide.Left);
        var rightChild = children.FirstOrDefault(c => c.Side == TreeSide.Right);

        var dto = new DualTreeNodeDto
        {
            MemberId   = nodeId,
            FullName   = profile is not null ? $"{profile.FirstName} {profile.LastName}" : nodeId,
            StatusCode = statusCode,
            Points     = stat?.DualTeamPoints ?? 0,
            LeftChild  = leftChild  is not null ? MapChild(leftChild.MemberId)  : null,
            RightChild = rightChild is not null ? MapChild(rightChild.MemberId) : null
        };

        return Ok(ApiResponse<DualTreeNodeDto>.Ok(dto));
    }

    // ─── Binary Tree: Stats (leg points) ────────────────────────────────────
    [HttpGet("dual-tree/stats/{statsId}")]
    public async Task<IActionResult> GetTreeStats(
        string memberId,
        string statsId,
        CancellationToken ct = default)
    {
        var node = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == statsId, ct);

        var dto = new DualTreeStatsDto
        {
            LeftLegPoints  = (int)(node?.LeftLegPoints  ?? 0),
            RightLegPoints = (int)(node?.RightLegPoints ?? 0)
        };

        return Ok(ApiResponse<DualTreeStatsDto>.Ok(dto));
    }

    // ─── DTOs ────────────────────────────────────────────────────────────────
    public class DualTeamMemberDto
    {
        public string   MemberId             { get; set; } = string.Empty;
        public string   FullName             { get; set; } = string.Empty;
        public int      Level                { get; set; }
        public string   Leg                  { get; set; } = string.Empty;
        public string   Country              { get; set; } = string.Empty;
        public string   SponsorName          { get; set; } = string.Empty;
        public string   DualTeamUplineName   { get; set; } = string.Empty;
        public string   BizCenterStatus      { get; set; } = string.Empty;
        public string   MembershipStatus     { get; set; } = string.Empty;
        public string   QualifiedStatus      { get; set; } = string.Empty;
        public string   MembershipName       { get; set; } = string.Empty;
        public string   RankName             { get; set; } = string.Empty;
        public DateTime? RankDate            { get; set; }
        public string   LifetimeRankName     { get; set; } = string.Empty;
        public int      NextRankPercent      { get; set; }
        public int      QualificationPoints  { get; set; }
        public int      EnrollmentTeamPoints { get; set; }
        public int      LeftTeamPoints       { get; set; }
        public int      RightTeamPoints      { get; set; }
        public DateTime JoinDate             { get; set; }
    }

    public class DualTreeNodeDto
    {
        public string        MemberId   { get; set; } = string.Empty;
        public string        FullName   { get; set; } = string.Empty;
        public string        StatusCode { get; set; } = "Q";
        public int           Points     { get; set; }
        public DualChildDto? LeftChild  { get; set; }
        public DualChildDto? RightChild { get; set; }
    }

    public class DualChildDto
    {
        public string MemberId   { get; set; } = string.Empty;
        public string FullName   { get; set; } = string.Empty;
        public string StatusCode { get; set; } = "Q";
        public int    Points     { get; set; }
        public bool   HasLeft    { get; set; }
        public bool   HasRight   { get; set; }
    }

    public class DualTreeStatsDto
    {
        public int LeftLegPoints  { get; set; }
        public int RightLegPoints { get; set; }
    }
}
