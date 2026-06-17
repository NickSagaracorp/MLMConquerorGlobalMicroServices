using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <inheritdoc />
public class DualTeamService : IDualTeamService
{
    private readonly AppDbContext            _db;
    private readonly IRankComputationService _ranks;

    public DualTeamService(AppDbContext db, IRankComputationService ranks)
    {
        _db    = db;
        _ranks = ranks;
    }

    /// <summary>String columns the dual-team grid search box matches against.</summary>
    private static readonly string[] MyTeamSearchableFields =
    {
        nameof(DualTeamMyTeamMemberView.MemberId),
        nameof(DualTeamMyTeamMemberView.FullName),
        nameof(DualTeamMyTeamMemberView.Email),
        nameof(DualTeamMyTeamMemberView.Phone),
        nameof(DualTeamMyTeamMemberView.Country),
        nameof(DualTeamMyTeamMemberView.Leg),
        nameof(DualTeamMyTeamMemberView.SponsorFullName),
        nameof(DualTeamMyTeamMemberView.DualUplineFullName),
        nameof(DualTeamMyTeamMemberView.AccountStatus),
        nameof(DualTeamMyTeamMemberView.MembershipStatus),
        nameof(DualTeamMyTeamMemberView.MembershipLevelName),
        nameof(DualTeamMyTeamMemberView.CurrentRankName),
        nameof(DualTeamMyTeamMemberView.LifetimeRankName),
    };

    /// <summary>DB columns the dual-team grid can search at the DB (present in the light row).</summary>
    private static readonly string[] DualRowSearchableFields =
    {
        nameof(DualTeamMyTeamMemberView.MemberId),
        nameof(DualTeamMyTeamMemberView.FullName),
        nameof(DualTeamMyTeamMemberView.Email),
        nameof(DualTeamMyTeamMemberView.Phone),
        nameof(DualTeamMyTeamMemberView.Country),
        nameof(DualTeamMyTeamMemberView.AccountStatus),
    };

    public async Task<PagedResult<DualTeamMyTeamMemberView>> GetMyTeamAsync(
        string memberId, int page, int pageSize, string? search,
        DateTime? from, DateTime? to,
        CancellationToken ct = default)
    {
        var myNode = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == memberId, ct);
        if (myNode is null) return new PagedResult<DualTeamMyTeamMemberView>();

        var q = BuildMyTeamRowQueryable(myNode.HierarchyPath, memberId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            q = q.Where(r => r.FullName.ToLower().Contains(s)
                          || r.MemberId.ToLower().Contains(s)
                          || (r.Email != null && r.Email.ToLower().Contains(s)));
        }
        if (from.HasValue) q = q.Where(r => r.EnrollDate >= from.Value);
        if (to.HasValue)   q = q.Where(r => r.EnrollDate <= to.Value);

        var total = await q.CountAsync(ct);
        var rows  = await q.OrderByDescending(r => r.EnrollDate)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var items = await BuildDualViewsForPageAsync(rows, myNode.HierarchyPath, memberId, ct);
        return new PagedResult<DualTeamMyTeamMemberView>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        };
    }

    /// <summary>
    /// Server-side grid read over the viewer's binary subtree. Search/filter/sort/COUNT/page
    /// run in SQL against a LIGHT row projection (profile columns + the node's path), so only
    /// the requested page (≤ pageSize) is materialized — the previous version loaded the whole
    /// subtree (120k+ rows × 7 tables) and paged in memory. Leg/Level + the enriched columns
    /// (rank/membership/sponsor/points) are computed for the page only in
    /// <see cref="BuildDualViewsForPageAsync"/>; grid ops on those non-projected columns are
    /// ignored by the grid helper (GetProperty returns null) rather than scanning 120k rows.
    /// </summary>
    public async Task<PagedResult<DualTeamMyTeamMemberView>> GetMyTeamGridAsync(
        string memberId, GridDataRequest request, CancellationToken ct = default)
    {
        var myNode = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == memberId, ct);
        if (myNode is null) return new PagedResult<DualTeamMyTeamMemberView>();

        // Ensure a deterministic DB sort even if the request only sorts on non-projected columns.
        if (!request.Sorts.Any(s => DualRowSearchableFields.Contains(s.Field, StringComparer.OrdinalIgnoreCase)
                                 || string.Equals(s.Field, nameof(DualMyTeamRow.EnrollDate), StringComparison.OrdinalIgnoreCase)))
            request.Sorts.Add(new GridSort { Field = nameof(DualMyTeamRow.EnrollDate), Direction = "desc" });

        var q     = BuildMyTeamRowQueryable(myNode.HierarchyPath, memberId);
        var paged = await q.ToGridResultAsync(request, DualRowSearchableFields, ct: ct);

        var items = await BuildDualViewsForPageAsync(paged.Items.ToList(), myNode.HierarchyPath, memberId, ct);
        return new PagedResult<DualTeamMyTeamMemberView>
        {
            Items = items, TotalCount = paged.TotalCount, Page = paged.Page, PageSize = paged.PageSize
        };
    }

    /// <summary>Light, fully DB-translatable row: profile columns + the node's binary-tree path
    /// (needed to derive Leg/Level for the page). Property names that the grid searches/sorts on
    /// match <see cref="DualTeamMyTeamMemberView"/>; non-projected names are ignored by the grid.</summary>
    private sealed class DualMyTeamRow
    {
        public string    MemberId        { get; set; } = string.Empty;
        public string    FullName        { get; set; } = string.Empty;
        public string?   Email           { get; set; }
        public string?   Phone           { get; set; }
        public string    Country         { get; set; } = string.Empty;
        public DateTime  EnrollDate      { get; set; }
        public string?   SponsorMemberId { get; set; }
        public string    AccountStatus   { get; set; } = string.Empty;
        public string    HierarchyPath   { get; set; } = string.Empty;
        public string?   ParentMemberId  { get; set; }
        public decimal   LeftLegPoints   { get; set; }
        public decimal   RightLegPoints  { get; set; }
    }

    private IQueryable<DualMyTeamRow> BuildMyTeamRowQueryable(string pathPrefix, string memberId)
    {
        return
            from d in _db.DualTeamTree.AsNoTracking()
            where d.HierarchyPath.StartsWith(pathPrefix) && d.MemberId != memberId
            join m in _db.MemberProfiles.AsNoTracking() on d.MemberId equals m.MemberId
            select new DualMyTeamRow
            {
                MemberId        = m.MemberId,
                FullName        = m.FirstName + " " + m.LastName,
                Email           = m.Email,
                Phone           = m.Phone,
                Country         = m.Country,
                EnrollDate      = m.EnrollDate,
                SponsorMemberId = m.SponsorMemberId,
                AccountStatus   = m.Status.ToString(),
                HierarchyPath   = d.HierarchyPath,
                ParentMemberId  = d.ParentMemberId,
                LeftLegPoints   = d.LeftLegPoints,
                RightLegPoints  = d.RightLegPoints
            };
    }

    /// <summary>Build the full enriched dual-team views for one page of rows (≤ pageSize):
    /// derive Leg (from the gateway child's Side) + Level (path depth) and pull subscriptions /
    /// ranks / stats / sponsor+upline names via Contains(~20 ids) — bounded regardless of
    /// downline size.</summary>
    private async Task<List<DualTeamMyTeamMemberView>> BuildDualViewsForPageAsync(
        List<DualMyTeamRow> rows, string pathPrefix, string memberId, CancellationToken ct)
    {
        if (rows.Count == 0) return new List<DualTeamMyTeamMemberView>();

        var rootDepth = SegmentCount(pathPrefix);
        var ids       = rows.Select(r => r.MemberId).ToList();

        // Gateway children of the viewer (≤2): their Side is the leg of any descendant whose
        // path passes through them (the first segment after the viewer's path).
        var legByGatewayId = await _db.DualTeamTree.AsNoTracking()
            .Where(d => d.ParentMemberId == memberId)
            .Select(d => new { d.MemberId, d.Side })
            .ToDictionaryAsync(d => d.MemberId, d => d.Side, ct);

        var subscriptions = await _db.MembershipSubscriptions.AsNoTracking()
            .Include(s => s.MembershipLevel)
            .Where(s => ids.Contains(s.MemberId) && s.SubscriptionStatus != MembershipStatus.Cancelled)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(ct);
        var subMap = subscriptions.GroupBy(s => s.MemberId).ToDictionary(g => g.Key, g => g.First());

        var rankHistories = await _db.MemberRankHistories.AsNoTracking()
            .Include(r => r.RankDefinition)
            .Where(r => ids.Contains(r.MemberId))
            .ToListAsync(ct);
        var currentRankMap = rankHistories.GroupBy(r => r.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.AchievedAt).First());
        var lifetimeRankMap = rankHistories.GroupBy(r => r.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.RankDefinition?.SortOrder ?? 0).First());

        var statsMap = await _db.MemberStatistics.AsNoTracking()
            .Where(s => ids.Contains(s.MemberId))
            .ToDictionaryAsync(s => s.MemberId, ct);

        var resolveIds = rows.Where(r => r.SponsorMemberId != null).Select(r => r.SponsorMemberId!)
            .Union(rows.Where(r => r.ParentMemberId != null).Select(r => r.ParentMemberId!))
            .Distinct().ToList();
        var nameMap = await _db.MemberProfiles.AsNoTracking()
            .Where(m => resolveIds.Contains(m.MemberId))
            .Select(m => new { m.MemberId, FullName = m.FirstName + " " + m.LastName })
            .ToDictionaryAsync(m => m.MemberId, m => m.FullName, ct);

        var allRanks = await _db.RankDefinitions.AsNoTracking()
            .Include(r => r.Requirements).OrderBy(r => r.SortOrder).ToListAsync(ct);

        return rows.Select(r =>
        {
            subMap.TryGetValue(r.MemberId, out var sub);
            currentRankMap.TryGetValue(r.MemberId, out var cr);
            lifetimeRankMap.TryGetValue(r.MemberId, out var lr);
            statsMap.TryGetValue(r.MemberId, out var stat);
            nameMap.TryGetValue(r.SponsorMemberId ?? "", out var sponsorName);
            nameMap.TryGetValue(r.ParentMemberId ?? "", out var uplineName);

            var rel = r.HierarchyPath.Length > pathPrefix.Length ? r.HierarchyPath[pathPrefix.Length..] : string.Empty;
            var firstSeg = rel.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            var leg = firstSeg is not null && legByGatewayId.TryGetValue(firstSeg, out var side)
                ? side.ToString() : string.Empty;

            var currentSortOrder = cr?.RankDefinition?.SortOrder ?? 0;
            var nextRank = allRanks.FirstOrDefault(rk => rk.SortOrder > currentSortOrder);
            int pct = 0;
            if (nextRank is not null)
            {
                var req = nextRank.Requirements.OrderBy(rk => rk.LevelNo).FirstOrDefault();
                if (req is not null && req.TeamPoints > 0)
                    pct = Math.Min(100, (int)((stat?.DualTeamPoints ?? 0) * 100.0 / req.TeamPoints));
            }
            else if (cr is not null) pct = 100;

            return new DualTeamMyTeamMemberView
            {
                MemberId             = r.MemberId,
                FullName             = r.FullName,
                Email                = r.Email,
                Phone                = r.Phone,
                Country              = r.Country,
                Level                = SegmentCount(r.HierarchyPath) - rootDepth,
                Leg                  = leg,
                EnrollDate           = r.EnrollDate,
                SponsorMemberId      = r.SponsorMemberId,
                SponsorFullName      = sponsorName,
                DualUplineMemberId   = r.ParentMemberId,
                DualUplineFullName   = uplineName,
                AccountStatus        = r.AccountStatus,
                MembershipStatus     = sub?.SubscriptionStatus.ToString() ?? "None",
                IsQualified          = sub?.SubscriptionStatus == MembershipStatus.Active,
                MembershipLevelName  = sub?.MembershipLevel?.Name,
                CurrentRankName      = cr?.RankDefinition?.Name,
                RankDate             = cr?.AchievedAt,
                LifetimeRankName     = lr?.RankDefinition?.Name,
                NextRankPercent      = pct,
                QualificationPoints  = stat?.PersonalPoints   ?? 0,
                EnrollmentTeamPoints = stat?.EnrollmentPoints ?? 0,
                LeftTeamPoints       = r.LeftLegPoints,
                RightTeamPoints      = r.RightLegPoints
            };
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<DualTreeStatsView> GetDualTreeStatsAsync(string memberId, CancellationToken ct = default)
    {
        // Denormalised leg totals — O(1), maintained by the placement engine and shared with
        // rank qualification. (Never recompute by scanning the subtree.)
        var node = await _db.DualTeamTree.AsNoTracking()
            .Where(d => d.MemberId == memberId)
            .Select(d => new { d.LeftLegPoints, d.RightLegPoints })
            .FirstOrDefaultAsync(ct);

        // Per-leg cap toward the NEXT rank — same rule the rank engine applies for qualification:
        // cap = MaxTeamPointsPerBranch × nextRank.TeamPoints (0 ⇒ DT does not apply at that rank).
        var summary     = await _ranks.GetSummaryAsync(memberId, ct);
        var nextRankDef = summary.NextRankSortOrder > 0
            ? await _db.RankDefinitions.AsNoTracking()
                .Include(r => r.Requirements)
                .FirstOrDefaultAsync(r => r.SortOrder == summary.NextRankSortOrder, ct)
            : null;
        var req = nextRankDef?.Requirements.OrderBy(r => r.LevelNo).FirstOrDefault();
        var cap = req is { TeamPoints: > 0, MaxTeamPointsPerBranch: > 0 }
            ? (int)Math.Round(req.MaxTeamPointsPerBranch * req.TeamPoints)
            : 0;

        return new DualTreeStatsView
        {
            LeftLegPoints  = node?.LeftLegPoints  ?? 0,
            RightLegPoints = node?.RightLegPoints ?? 0,
            NextRankLegCap = cap,
            NextRankName   = nextRankDef?.Name
        };
    }

    /// <inheritdoc />
    public async Task<List<DualTreeSearchMatchView>> SearchBinarySubtreeAsync(
        string rootMemberId, string? term, int take = 25, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term)) return new();
        take = Math.Clamp(take, 1, 100);

        var rootPath = await _db.DualTeamTree.AsNoTracking()
            .Where(d => d.MemberId == rootMemberId).Select(d => d.HierarchyPath).FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(rootPath)) return new();

        var s = term.Trim().ToLower();
        var matches = await (
            from d in _db.DualTeamTree.AsNoTracking()
            where d.HierarchyPath.StartsWith(rootPath) && d.MemberId != rootMemberId
            join m in _db.MemberProfiles.AsNoTracking() on d.MemberId equals m.MemberId
            where m.MemberId.ToLower().Contains(s) || (m.FirstName + " " + m.LastName).ToLower().Contains(s)
            orderby d.HierarchyPath.Length      // shallowest matches first
            select new { d.MemberId, d.HierarchyPath, m.FirstName, m.LastName }
        ).Take(take).ToListAsync(ct);

        if (matches.Count == 0) return new();

        var rootSegLen   = SegmentCount(rootPath);
        var legByGateway = (await _db.DualTeamTree.AsNoTracking()
                .Where(d => d.ParentMemberId == rootMemberId)
                .Select(d => new { d.MemberId, d.Side })
                .ToListAsync(ct))
            .ToDictionary(g => g.MemberId, g => g.Side);

        // Resolve names for every id on every match's path-below-root (one batched query).
        var pathIds = matches
            .SelectMany(mt => mt.HierarchyPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(rootSegLen))
            .Distinct().ToList();
        var names = await _db.MemberProfiles.AsNoTracking()
            .Where(p => pathIds.Contains(p.MemberId))
            .Select(p => new { p.MemberId, Name = p.FirstName + " " + p.LastName })
            .ToDictionaryAsync(x => x.MemberId, x => x.Name.Trim(), ct);

        return matches.Select(mt =>
        {
            var afterRoot = mt.HierarchyPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(rootSegLen).ToList();
            var leg = afterRoot.Count > 0 && legByGateway.TryGetValue(afterRoot[0], out var side) ? side.ToString() : string.Empty;
            return new DualTreeSearchMatchView
            {
                MemberId = mt.MemberId,
                FullName = $"{mt.FirstName} {mt.LastName}".Trim(),
                Leg      = leg,
                Depth    = afterRoot.Count,
                Path     = afterRoot.Select(id => new DualTreePathNodeView
                {
                    MemberId = id,
                    FullName = names.GetValueOrDefault(id, id)
                }).ToList()
            };
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<DualTreeNavTargetView?> GetDeepestNodeAsync(
        string rootMemberId, TreeSide side, CancellationToken ct = default)
    {
        var gateway = await _db.DualTeamTree.AsNoTracking()
            .Where(d => d.ParentMemberId == rootMemberId && d.Side == side)
            .Select(d => new { d.MemberId, d.HierarchyPath })
            .FirstOrDefaultAsync(ct);
        if (gateway is null) return null;

        // Deepest descendant on this leg — longest path string is the deepest (each level adds a
        // segment). Approximate but monotonic with depth, which is all a "jump to deepest" needs.
        var deepest = await _db.DualTeamTree.AsNoTracking()
            .Where(d => d.HierarchyPath.StartsWith(gateway.HierarchyPath))
            .OrderByDescending(d => d.HierarchyPath.Length)
            .Select(d => new { d.MemberId, d.HierarchyPath })
            .FirstOrDefaultAsync(ct)
            ?? new { gateway.MemberId, gateway.HierarchyPath };

        var rootPath = await _db.DualTeamTree.AsNoTracking()
            .Where(d => d.MemberId == rootMemberId).Select(d => d.HierarchyPath).FirstOrDefaultAsync(ct) ?? string.Empty;
        var rootSegLen = SegmentCount(rootPath);

        var afterRoot = deepest.HierarchyPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(rootSegLen).ToList();
        var names = await _db.MemberProfiles.AsNoTracking()
            .Where(p => afterRoot.Contains(p.MemberId))
            .Select(p => new { p.MemberId, Name = p.FirstName + " " + p.LastName })
            .ToDictionaryAsync(x => x.MemberId, x => x.Name.Trim(), ct);

        return new DualTreeNavTargetView
        {
            MemberId = deepest.MemberId,
            FullName = names.GetValueOrDefault(deepest.MemberId, deepest.MemberId),
            Depth    = afterRoot.Count,
            Path     = afterRoot.Select(id => new DualTreePathNodeView
            {
                MemberId = id,
                FullName = names.GetValueOrDefault(id, id)
            }).ToList()
        };
    }

    private static int SegmentCount(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;

    public async Task<List<DualLegRowView>> GetResidualLegsAsync(
        string memberId, CancellationToken ct = default)
    {
        var rows = new List<DualLegRowView>();

        var viewerNode = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == memberId, ct);
        if (viewerNode is null) return rows;

        var viewerProfile = await _db.MemberProfiles.AsNoTracking()
            .Where(m => m.MemberId == memberId)
            .Select(m => new { m.FirstName, m.LastName })
            .FirstOrDefaultAsync(ct);

        // Two direct binary children: gateway-left and gateway-right. Either
        // (or both) may be missing on a fresh member; we just emit whichever
        // exist so the table degrades gracefully.
        var directChildren = await _db.DualTeamTree.AsNoTracking()
            .Where(d => d.ParentMemberId == memberId)
            .Select(d => new { d.MemberId, d.Side, d.LeftLegPoints, d.RightLegPoints })
            .ToListAsync(ct);
        var leftGateway  = directChildren.FirstOrDefault(d => d.Side == TreeSide.Left);
        var rightGateway = directChildren.FirstOrDefault(d => d.Side == TreeSide.Right);

        var gatewayIds = directChildren.Select(d => d.MemberId).ToList();
        var gatewayNames = gatewayIds.Count > 0
            ? await _db.MemberProfiles.AsNoTracking()
                .Where(m => gatewayIds.Contains(m.MemberId))
                .Select(m => new { m.MemberId, FullName = (m.FirstName + " " + m.LastName).Trim() })
                .ToDictionaryAsync(m => m.MemberId, m => m.FullName, ct)
            : new Dictionary<string, string>();

        // Per-leg DT cap mirrors the rank engine — MaxTeamPointsPerBranch ×
        // rank.TeamPoints. Threshold = 0 means the dimension does not apply
        // at this rank (Silver/Gold/Platinum) so the per-row donut percent
        // and eligible value collapse to 0; the UI hides the donut in that
        // case to avoid misleading "0/0 progress" math.
        var summary = await _ranks.GetSummaryAsync(memberId, ct);
        var rankReqs = await _db.RankDefinitions.AsNoTracking()
            .Include(r => r.Requirements)
            .Where(r => r.SortOrder == summary.CurrentRankSortOrder
                     || r.SortOrder == summary.NextRankSortOrder)
            .ToListAsync(ct);
        var currentReq = rankReqs.FirstOrDefault(r => r.SortOrder == summary.CurrentRankSortOrder)
            ?.Requirements.OrderBy(r => r.LevelNo).FirstOrDefault();
        var nextReq    = rankReqs.FirstOrDefault(r => r.SortOrder == summary.NextRankSortOrder)
            ?.Requirements.OrderBy(r => r.LevelNo).FirstOrDefault();

        static int LegCap(RankRequirement? req) =>
            req is { TeamPoints: > 0, MaxTeamPointsPerBranch: > 0 }
                ? (int)Math.Round(req.MaxTeamPointsPerBranch * req.TeamPoints)
                : 0;

        var currentLegCap   = LegCap(currentReq);
        var nextLegCap      = LegCap(nextReq);
        var currentThreshold = currentReq?.TeamPoints ?? 0;
        var nextThreshold    = nextReq?.TeamPoints    ?? 0;

        var leftLeg  = (int)viewerNode.LeftLegPoints;
        var rightLeg = (int)viewerNode.RightLegPoints;

        // ── Root row ────────────────────────────────────────────────────────
        // QP = sum of both legs. Eligible = capped sum, then capped at the
        // rank's total threshold (so root donut tops out at 100% when both
        // legs are full).
        var rootEligibleCurrent = SumCapped(leftLeg, rightLeg, currentLegCap, currentThreshold);
        var rootEligibleNext    = SumCapped(leftLeg, rightLeg, nextLegCap,    nextThreshold);

        rows.Add(new DualLegRowView
        {
            MemberId                  = memberId,
            FullName                  = viewerProfile is null
                ? memberId
                : $"{viewerProfile.FirstName} {viewerProfile.LastName}".Trim(),
            Leg                       = "Root",
            RankName                  = summary.CurrentRankName,
            QualificationPoints       = leftLeg + rightLeg,
            CurrentRankEligiblePoints = rootEligibleCurrent,
            CurrentRankEligiblePct    = currentThreshold > 0
                ? Math.Min(100, rootEligibleCurrent * 100 / currentThreshold) : 0,
            NextRankEligiblePoints    = rootEligibleNext,
            NextRankEligiblePct       = nextThreshold > 0
                ? Math.Min(100, rootEligibleNext * 100 / nextThreshold) : 0
        });

        // ── Left gateway row ────────────────────────────────────────────────
        if (leftGateway is not null)
        {
            var legPts            = leftLeg;
            var eligibleCurrent   = currentLegCap > 0 ? Math.Min(legPts, currentLegCap) : 0;
            var eligibleNext      = nextLegCap    > 0 ? Math.Min(legPts, nextLegCap)    : 0;
            rows.Add(new DualLegRowView
            {
                MemberId                  = leftGateway.MemberId,
                FullName                  = gatewayNames.GetValueOrDefault(leftGateway.MemberId, leftGateway.MemberId),
                Leg                       = "Left",
                QualificationPoints       = legPts,
                CurrentRankEligiblePoints = eligibleCurrent,
                CurrentRankEligiblePct    = currentLegCap > 0
                    ? Math.Min(100, eligibleCurrent * 100 / currentLegCap) : 0,
                NextRankEligiblePoints    = eligibleNext,
                NextRankEligiblePct       = nextLegCap > 0
                    ? Math.Min(100, eligibleNext * 100 / nextLegCap) : 0
            });
        }

        // ── Right gateway row ───────────────────────────────────────────────
        if (rightGateway is not null)
        {
            var legPts            = rightLeg;
            var eligibleCurrent   = currentLegCap > 0 ? Math.Min(legPts, currentLegCap) : 0;
            var eligibleNext      = nextLegCap    > 0 ? Math.Min(legPts, nextLegCap)    : 0;
            rows.Add(new DualLegRowView
            {
                MemberId                  = rightGateway.MemberId,
                FullName                  = gatewayNames.GetValueOrDefault(rightGateway.MemberId, rightGateway.MemberId),
                Leg                       = "Right",
                QualificationPoints       = legPts,
                CurrentRankEligiblePoints = eligibleCurrent,
                CurrentRankEligiblePct    = currentLegCap > 0
                    ? Math.Min(100, eligibleCurrent * 100 / currentLegCap) : 0,
                NextRankEligiblePoints    = eligibleNext,
                NextRankEligiblePct       = nextLegCap > 0
                    ? Math.Min(100, eligibleNext * 100 / nextLegCap) : 0
            });
        }

        return rows;
    }

    private static int SumCapped(int leftLeg, int rightLeg, int legCap, int threshold)
    {
        if (legCap <= 0 || threshold <= 0) return 0;
        var summed = Math.Min(leftLeg, legCap) + Math.Min(rightLeg, legCap);
        return Math.Min(summed, threshold);
    }

    public async Task<List<DualLegMonthlyPointView>> GetDualTeamHistoryAsync(
        string memberId, int months, CancellationToken ct = default)
    {
        if (months <= 0) months = 6;

        // Anchor on the first day of the current UTC month and walk backwards
        // so the rightmost (latest) bucket on the chart is always "this month".
        var nowUtc    = DateTime.UtcNow;
        var anchor    = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var earliest  = anchor.AddMonths(-(months - 1));

        var snapshots = await _db.MemberStatisticHistories.AsNoTracking()
            .Where(h => h.MemberId == memberId
                     && (h.SnapshotYear  > earliest.Year
                         || (h.SnapshotYear  == earliest.Year
                             && h.SnapshotMonth >= earliest.Month)))
            .Select(h => new { h.SnapshotYear, h.SnapshotMonth, h.LeftLegPoints, h.RightLegPoints })
            .ToListAsync(ct);

        var bucketed = snapshots.ToDictionary(
            s => (s.SnapshotYear, s.SnapshotMonth),
            s => (s.LeftLegPoints, s.RightLegPoints));

        // Live values for the current month — the snapshot is end-of-day so
        // it would render yesterday's totals, not today's. Falling back to
        // DualTeamTree keeps the latest bar honest.
        var liveNode = await _db.DualTeamTree.AsNoTracking()
            .Where(d => d.MemberId == memberId)
            .Select(d => new { d.LeftLegPoints, d.RightLegPoints })
            .FirstOrDefaultAsync(ct);

        var series = new List<DualLegMonthlyPointView>(months);
        for (var i = 0; i < months; i++)
        {
            var d         = earliest.AddMonths(i);
            var isCurrent = d.Year == nowUtc.Year && d.Month == nowUtc.Month;
            decimal left = 0m, right = 0m;
            if (isCurrent && liveNode is not null)
            {
                left  = liveNode.LeftLegPoints;
                right = liveNode.RightLegPoints;
            }
            else if (bucketed.TryGetValue((d.Year, d.Month), out var snap))
            {
                left  = snap.LeftLegPoints;
                right = snap.RightLegPoints;
            }

            series.Add(new DualLegMonthlyPointView
            {
                Year           = d.Year,
                Month          = d.Month,
                LeftLegPoints  = left,
                RightLegPoints = right
            });
        }
        return series;
    }
}
