using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Placement;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
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
    private readonly MLMConquerorGlobalEdition.Repository.Services.Ranks.IRankComputationService _ranks;

    public AdminMemberDualTeamController(
        AppDbContext db,
        MLMConquerorGlobalEdition.Repository.Services.Ranks.IRankComputationService ranks)
    {
        _db    = db;
        _ranks = ranks;
    }

    // ─── Dual Team My Team (rich DTO shape used by Admin Dual Team grid) ────
    /// <summary>
    /// Route: GET api/v1/admin/members/{memberId}/team/my-team
    /// Mirrors the BizCenter endpoint <c>/api/v1/bizcenter/team/dual-tree/my-team</c>
    /// but targets the path <paramref name="memberId"/> instead of the current
    /// user. Both share the <see cref="MLMConquerorGlobalEdition.Repository.Services.Teams.IDualTeamService"/>
    /// so admin and member views never drift.
    /// </summary>
    [HttpGet("my-team")]
    public async Task<IActionResult> GetMyTeam(
        string memberId,
        [FromServices] MLMConquerorGlobalEdition.Repository.Services.Teams.IDualTeamService dualTeamService,
        [FromQuery] int       page     = 1,
        [FromQuery] int       pageSize = 20,
        [FromQuery] string?   search   = null,
        [FromQuery] DateTime? from     = null,
        [FromQuery] DateTime? to       = null,
        CancellationToken ct = default)
    {
        var view = await dualTeamService.GetMyTeamAsync(memberId, page, pageSize, search, from, to, ct);
        return Ok(ApiResponse<PagedResult<MLMConquerorGlobalEdition.Repository.Services.Teams.DualTeamMyTeamMemberView>>.Ok(view));
    }

    /// <summary>
    /// Route: POST api/v1/admin/members/{memberId}/team/my-team/grid
    /// Server-side grid read (search · per-column filter · sort · page) over the
    /// member's whole binary subtree, so the grid finds matches on any page.
    /// </summary>
    [HttpPost("my-team/grid")]
    public async Task<IActionResult> GetMyTeamGrid(
        string memberId,
        [FromBody] GridDataRequest request,
        [FromServices] MLMConquerorGlobalEdition.Repository.Services.Teams.IDualTeamService dualTeamService,
        CancellationToken ct = default)
    {
        var view = await dualTeamService.GetMyTeamGridAsync(memberId, request, ct);
        return Ok(ApiResponse<PagedResult<MLMConquerorGlobalEdition.Repository.Services.Teams.DualTeamMyTeamMemberView>>.Ok(view));
    }

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
        var rootDepth  = pathPrefix.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;

        // 2. Page the binary subtree at the DB (join DualTeamTree subtree → profiles), instead
        //    of loading ALL ~120k subtree nodes into memory + Contains(allIds). Only the page's
        //    rows (≤ pageSize) are materialized; level/leg come from the page rows' own paths.
        var baseQuery =
            from d in _db.DualTeamTree.AsNoTracking()
            where d.HierarchyPath.StartsWith(pathPrefix) && d.MemberId != memberId
            join m in _db.MemberProfiles.AsNoTracking() on d.MemberId equals m.MemberId
            select new
            {
                m.MemberId, m.FirstName, m.LastName, m.Country, m.SponsorMemberId, m.Status, m.CreationDate,
                d.HierarchyPath, d.ParentMemberId, d.Side, d.LeftLegPoints, d.RightLegPoints
            };

        if (!string.IsNullOrWhiteSpace(search))
            baseQuery = baseQuery.Where(x =>
                x.FirstName.Contains(search) || x.LastName.Contains(search) || x.MemberId.Contains(search));

        var totalCount = await baseQuery.CountAsync(ct);

        var pageRows = await baseQuery
            .OrderBy(x => x.CreationDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        if (pageRows.Count == 0)
            return Ok(ApiResponse<PagedResult<DualTeamMemberDto>>.Ok(
                new PagedResult<DualTeamMemberDto> { Items = new List<DualTeamMemberDto>(), TotalCount = totalCount, Page = page, PageSize = pageSize }));

        var profiles = pageRows
            .Select(x => new { x.MemberId, x.FirstName, x.LastName, x.Country, x.SponsorMemberId, x.Status, x.CreationDate })
            .ToList();
        var pageIds = profiles.Select(p => p.MemberId).ToHashSet();

        var levelMap = pageRows.ToDictionary(
            x => x.MemberId,
            x => x.HierarchyPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length - rootDepth);
        var nodeMap = pageRows.ToDictionary(
            x => x.MemberId,
            x => new { x.ParentMemberId, x.Side, x.LeftLegPoints, x.RightLegPoints });

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

        // 8b. Per-leg DT cap for the viewer's current and next rank.
        //     cap = MaxTeamPointsPerBranch × rank.TeamPoints — the same rule
        //     the rank engine applies for qualification. cap = 0 means the DT
        //     dimension does not apply at this rank (Silver/Gold/Platinum) so
        //     the row donuts collapse to N/A on the client.
        var viewerSummary = await _ranks.GetSummaryAsync(memberId, ct);
        var currentRankDef = allRanks.FirstOrDefault(r => r.SortOrder == viewerSummary.CurrentRankSortOrder);
        var nextRankDef    = allRanks.FirstOrDefault(r => r.SortOrder >  viewerSummary.CurrentRankSortOrder);

        var legCapCurrent = CalcLegCap(currentRankDef);
        var legCapNext    = CalcLegCap(nextRankDef);

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

            // Per-leg DT eligibility for this member toward the viewer's rank.
            // The donut shows min(personal, leg_cap)/leg_cap; when leg_cap is 0
            // the dimension does not apply and the client renders "—".
            var personalPts          = stat?.PersonalPoints ?? 0;
            var eligibleCurrentPts   = legCapCurrent > 0 ? Math.Min(personalPts, legCapCurrent) : 0;
            var eligibleNextPts      = legCapNext    > 0 ? Math.Min(personalPts, legCapNext)    : 0;
            var eligibleCurrentPct   = legCapCurrent > 0
                ? Math.Min(100, eligibleCurrentPts * 100 / legCapCurrent) : 0;
            var eligibleNextPct      = legCapNext > 0
                ? Math.Min(100, eligibleNextPts * 100 / legCapNext) : 0;

            return new DualTeamMemberDto
            {
                MemberId                  = p.MemberId,
                FullName                  = $"{p.FirstName} {p.LastName}",
                Level                     = levelMap.TryGetValue(p.MemberId, out var lv) ? lv : 0,
                Leg                       = leg,
                Country                   = p.Country,
                SponsorName               = sponsorName ?? "—",
                DualTeamUplineName        = uplineName ?? "—",
                BizCenterStatus           = bizStatus,
                MembershipStatus          = memStatus,
                QualifiedStatus           = qualified,
                MembershipName            = sub != null && levels.TryGetValue(sub.MembershipLevelId, out var lvName) ? lvName : "—",
                RankName                  = currentRank?.Name ?? "—",
                RankDate                  = rank?.AchievedAt,
                LifetimeRankName          = currentRank?.Name ?? "—",
                NextRankPercent           = nextPct,
                QualificationPoints       = personalPts,
                CurrentRankEligiblePoints = eligibleCurrentPts,
                CurrentRankEligiblePct    = eligibleCurrentPct,
                NextRankEligiblePoints    = eligibleNextPts,
                NextRankEligiblePct       = eligibleNextPct,
                EnrollmentTeamPoints      = stat?.EnrollmentPoints ?? 0,
                LeftTeamPoints            = (int)(node?.LeftLegPoints ?? 0),
                RightTeamPoints           = (int)(node?.RightLegPoints ?? 0),
                JoinDate                  = p.CreationDate
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
    /// <summary>
    /// Dual-tree node — delegates to the shared <see cref="IDualTreeNodeService"/>
    /// so admin and bizcenter views always agree.
    /// </summary>
    [HttpGet("dual-tree/node/{nodeId}")]
    public async Task<IActionResult> GetTreeNode(
        string memberId,
        string nodeId,
        [FromServices] MLMConquerorGlobalEdition.Repository.Services.Teams.IDualTreeNodeService treeService,
        CancellationToken ct = default)
    {
        var view = await treeService.GetNodeAsync(nodeId, ct);
        return Ok(ApiResponse<MLMConquerorGlobalEdition.Repository.Services.Teams.DualTreeNodeView>.Ok(view));
    }

    // ─── Binary Tree: Stats (leg points) ────────────────────────────────────
    [HttpGet("dual-tree/stats/{statsId}")]
    public async Task<IActionResult> GetTreeStats(
        string memberId,
        string statsId,
        [FromServices] MLMConquerorGlobalEdition.Repository.Services.Teams.IDualTeamService dualTeamService,
        CancellationToken ct = default)
    {
        // Single source of truth shared with BizCenter — see IDualTeamService.GetDualTreeStatsAsync.
        var v = await dualTeamService.GetDualTreeStatsAsync(statsId, ct);
        var dto = new DualTreeStatsDto
        {
            LeftLegPoints  = (int)v.LeftLegPoints,
            RightLegPoints = (int)v.RightLegPoints,
            NextRankLegCap = v.NextRankLegCap,
            NextRankName   = v.NextRankName
        };

        return Ok(ApiResponse<DualTreeStatsDto>.Ok(dto));
    }

    /// <summary>
    /// Per-leg dual-team points cap for a rank — the same rule the rank engine
    /// applies for qualification: cap = MaxTeamPointsPerBranch × rank.TeamPoints.
    /// Returns 0 when the rank has no dual-team requirement (DT does not apply).
    /// </summary>
    private static int CalcLegCap(Domain.Entities.Rank.RankDefinition? rankDef)
    {
        var req = rankDef?.Requirements.OrderBy(r => r.LevelNo).FirstOrDefault();
        if (req is null || req.TeamPoints <= 0 || req.MaxTeamPointsPerBranch <= 0) return 0;
        return (int)Math.Round(req.MaxTeamPointsPerBranch * req.TeamPoints);
    }

    // ─── Residuals page legs feed ────────────────────────────────────────────
    /// <summary>GET .../team/dual-tree/legs — the three-row "Dual Team
    /// Members" feed used by the Residuals page (root + L gateway + R gateway).
    /// Distinct from /team/members which still serves the full subtree for
    /// token distribution and other downline pickers.</summary>
    [HttpGet("dual-tree/legs")]
    public async Task<IActionResult> GetResidualLegs(
        string memberId,
        [FromServices] MLMConquerorGlobalEdition.Repository.Services.Teams.IDualTeamService dualTeamService,
        CancellationToken ct = default)
    {
        var rows = await dualTeamService.GetResidualLegsAsync(memberId, ct);
        return Ok(ApiResponse<List<MLMConquerorGlobalEdition.Repository.Services.Teams.DualLegRowView>>.Ok(rows));
    }

    /// <summary>GET .../team/dual-tree/history?months=6 — last N monthly
    /// snapshots of L/R leg points for the Total Dual Team Points trend chart.
    /// Pulls from MemberStatisticHistory; the latest bucket is replaced with
    /// the live DualTeamTree values so today's bar reflects today's totals.</summary>
    [HttpGet("dual-tree/history")]
    public async Task<IActionResult> GetDualTeamHistory(
        string memberId,
        [FromServices] MLMConquerorGlobalEdition.Repository.Services.Teams.IDualTeamService dualTeamService,
        [FromQuery] int months = 6,
        CancellationToken ct = default)
    {
        var rows = await dualTeamService.GetDualTeamHistoryAsync(memberId, months, ct);
        return Ok(ApiResponse<List<MLMConquerorGlobalEdition.Repository.Services.Teams.DualLegMonthlyPointView>>.Ok(rows));
    }

    // ─── Binary Tree: Search ─────────────────────────────────────────────────
    /// <summary>GET .../team/dual-tree/search?term=&amp;take=25 — find nodes in the member's
    /// binary subtree by name or member id. Each hit carries its path from the root so the
    /// visualizer can open (drill to) that branch and highlight the match. Shared with BizCenter.</summary>
    [HttpGet("dual-tree/search")]
    public async Task<IActionResult> SearchTree(
        string memberId,
        [FromServices] MLMConquerorGlobalEdition.Repository.Services.Teams.IDualTeamService dualTeamService,
        [FromQuery] string? term = null,
        [FromQuery] int take = 25,
        CancellationToken ct = default)
    {
        var hits = await dualTeamService.SearchBinarySubtreeAsync(memberId, term, take, ct);
        return Ok(ApiResponse<List<MLMConquerorGlobalEdition.Repository.Services.Teams.DualTreeSearchMatchView>>.Ok(hits));
    }

    /// <summary>GET .../team/dual-tree/deepest?side=Left|Right — the deepest node on the given
    /// leg, with its path from the root, for the "jump to deepest left/right" navigation arrows.
    /// Returns null data when that leg is empty. Shared with BizCenter.</summary>
    [HttpGet("dual-tree/deepest")]
    public async Task<IActionResult> GetDeepest(
        string memberId,
        [FromServices] MLMConquerorGlobalEdition.Repository.Services.Teams.IDualTeamService dualTeamService,
        [FromQuery] Domain.Enums.TreeSide side,
        CancellationToken ct = default)
    {
        var target = await dualTeamService.GetDeepestNodeAsync(memberId, side, ct);
        return Ok(ApiResponse<MLMConquerorGlobalEdition.Repository.Services.Teams.DualTreeNavTargetView?>.Ok(target));
    }

    // ─── Available For Placement ─────────────────────────────────────────────
    /// <summary>
    /// Returns all ambassadors in the member's enrollment genealogy downline
    /// who are NOT yet placed in the dual tree — eligible to be placed by admin.
    /// Route: GET api/v1/admin/members/{memberId}/team/dual-tree/available-for-placement
    /// </summary>
    [HttpGet("dual-tree/available-for-placement")]
    public async Task<IActionResult> GetAvailableForPlacement(
        string memberId,
        [FromQuery] string? search = null,
        [FromQuery] int take = 200,
        CancellationToken ct = default)
    {
        // Unplaced ambassadors in the member's genealogy downline, search-filtered and capped.
        // The previous version materialized ALL ~120k downline ids, ran two Contains(allIds)
        // queries, then did an O(downline) StartsWith COUNT per candidate (O(downline×candidates)
        // → 32s / 500 for a top-rank member). Now: one JOIN over the subtree + NOT EXISTS for
        // "not placed", a cap, and a single batched floating-children count.
        take = Math.Clamp(take, 1, 500);
        var hierarchyFilter = $"/{memberId}/";

        var q =
            from g in _db.GenealogyTree.AsNoTracking()
            where g.HierarchyPath.Contains(hierarchyFilter)
            join m in _db.MemberProfiles.AsNoTracking() on g.MemberId equals m.MemberId
            where m.MemberId != memberId
               && m.MemberType == MemberType.Ambassador
               && !m.IsDeleted
               && !_db.DualTeamTree.Any(d => d.MemberId == m.MemberId)   // not yet placed
            select m;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            q = q.Where(m => m.FirstName.ToLower().Contains(s)
                          || m.LastName.ToLower().Contains(s)
                          || m.MemberId.ToLower().Contains(s));
        }

        var candidates = await q
            .OrderBy(m => m.FirstName).ThenBy(m => m.LastName)
            .Take(take)
            .Select(m => new PlacementCandidateDto
            {
                MemberId = m.MemberId,
                FullName = (m.FirstName + " " + m.LastName).Trim(),
                PhotoUrl = m.ProfilePhotoUrl
            })
            .ToListAsync(ct);

        // Floating descendants left by a prior unplacement: a single batched count of each
        // candidate's DIRECT dual children (indexed ParentMemberId) — non-zero only flags that
        // placing this candidate re-attaches a floating subtree. Replaces the per-candidate
        // O(downline) HierarchyPath.StartsWith scan.
        if (candidates.Count > 0)
        {
            var candIds = candidates.Select(c => c.MemberId).ToList();
            var floating = await _db.DualTeamTree.AsNoTracking()
                .Where(d => d.ParentMemberId != null && candIds.Contains(d.ParentMemberId))
                .GroupBy(d => d.ParentMemberId!)
                .Select(grp => new { Id = grp.Key, Cnt = grp.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.Cnt, ct);

            foreach (var c in candidates)
                c.FloatingSubtreeSize = floating.GetValueOrDefault(c.MemberId);
        }

        return Ok(ApiResponse<List<PlacementCandidateDto>>.Ok(candidates));
    }

    // ─── DTOs ────────────────────────────────────────────────────────────────
    public class DualTeamMemberDto
    {
        public string   MemberId                  { get; set; } = string.Empty;
        public string   FullName                  { get; set; } = string.Empty;
        public int      Level                     { get; set; }
        public string   Leg                       { get; set; } = string.Empty;
        public string   Country                   { get; set; } = string.Empty;
        public string   SponsorName               { get; set; } = string.Empty;
        public string   DualTeamUplineName        { get; set; } = string.Empty;
        public string   BizCenterStatus           { get; set; } = string.Empty;
        public string   MembershipStatus          { get; set; } = string.Empty;
        public string   QualifiedStatus           { get; set; } = string.Empty;
        public string   MembershipName            { get; set; } = string.Empty;
        public string   RankName                  { get; set; } = string.Empty;
        public DateTime? RankDate                 { get; set; }
        public string   LifetimeRankName          { get; set; } = string.Empty;
        public int      NextRankPercent           { get; set; }
        public int      QualificationPoints       { get; set; }
        /// <summary>Member's points capped at the viewer's current-rank per-leg
        /// DT cap, with 0 signaling "DT does not apply at this rank" (the
        /// client uses this to collapse the donut to "—").</summary>
        public int      CurrentRankEligiblePoints { get; set; }
        public int      CurrentRankEligiblePct    { get; set; }
        public int      NextRankEligiblePoints    { get; set; }
        public int      NextRankEligiblePct       { get; set; }
        public int      EnrollmentTeamPoints      { get; set; }
        public int      LeftTeamPoints            { get; set; }
        public int      RightTeamPoints           { get; set; }
        public DateTime JoinDate                  { get; set; }
    }

    public class DualTreeNodeDto
    {
        public string        MemberId       { get; set; } = string.Empty;
        public string        FullName       { get; set; } = string.Empty;
        public string        StatusCode     { get; set; } = "Q";
        public int           Points         { get; set; }
        public int           PersonalPoints { get; set; }
        public DualChildDto? LeftChild      { get; set; }
        public DualChildDto? RightChild     { get; set; }
    }

    public class DualChildDto
    {
        public string             MemberId       { get; set; } = string.Empty;
        public string             FullName       { get; set; } = string.Empty;
        public string             StatusCode     { get; set; } = string.Empty;
        public int                Points         { get; set; }
        public int                PersonalPoints { get; set; }
        public bool               HasLeft        { get; set; }
        public bool               HasRight       { get; set; }
        public DualGrandchildDto? LeftChild      { get; set; }
        public DualGrandchildDto? RightChild     { get; set; }
    }

    public class DualGrandchildDto
    {
        public string MemberId       { get; set; } = string.Empty;
        public string FullName       { get; set; } = string.Empty;
        public string StatusCode     { get; set; } = string.Empty;
        public int    Points         { get; set; }
        public int    PersonalPoints { get; set; }
        public bool   HasLeft        { get; set; }
        public bool   HasRight       { get; set; }
    }

    public class DualTreeStatsDto
    {
        public int     LeftLegPoints  { get; set; }
        public int     RightLegPoints { get; set; }
        /// <summary>Per-leg points cap to reach the next rank
        /// (MaxTeamPointsPerBranch × nextRank.TeamPoints). 0 ⇒ the dual-team
        /// dimension does not apply at the next rank — render the bar as N/A.</summary>
        public int     NextRankLegCap { get; set; }
        /// <summary>Name of the next rank the cap targets, or null when already at top rank.</summary>
        public string? NextRankName   { get; set; }
    }
}
