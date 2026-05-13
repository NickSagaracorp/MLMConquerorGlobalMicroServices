using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
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

    public async Task<PagedResult<DualTeamMyTeamMemberView>> GetMyTeamAsync(
        string memberId, int page, int pageSize, string? search,
        DateTime? from, DateTime? to,
        CancellationToken ct = default)
    {
        var myNode = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == memberId, ct);
        if (myNode is null) return new PagedResult<DualTeamMyTeamMemberView>();

        var pathPrefix = myNode.HierarchyPath;
        var rootDepth  = SegmentCount(pathPrefix);

        // Pull every node in the viewer's binary subtree (excluding self).
        var subtreeNodes = await _db.DualTeamTree.AsNoTracking()
            .Where(d => d.HierarchyPath.StartsWith(pathPrefix) && d.MemberId != memberId)
            .Select(d => new { d.MemberId, d.HierarchyPath, d.ParentMemberId, d.LeftLegPoints, d.RightLegPoints })
            .ToListAsync(ct);

        if (!subtreeNodes.Any()) return new PagedResult<DualTeamMyTeamMemberView>();

        var subtreeIds = subtreeNodes.Select(n => n.MemberId).ToList();

        // Direct binary children of the viewer; their Side determines the leg
        // any deeper descendant sits on (the first segment after the viewer's
        // path is the gateway-node, and that node's Side is the leg).
        var directChildren = await _db.DualTeamTree.AsNoTracking()
            .Where(d => d.ParentMemberId == memberId)
            .Select(d => new { d.MemberId, d.Side })
            .ToListAsync(ct);
        var legByGatewayId = directChildren.ToDictionary(d => d.MemberId, d => d.Side);

        // Build a fast lookup MemberId -> first-segment-after-prefix.
        var legMap = new Dictionary<string, string>(subtreeNodes.Count);
        var levelMap = new Dictionary<string, int>(subtreeNodes.Count);
        foreach (var n in subtreeNodes)
        {
            var rel = n.HierarchyPath.Length > pathPrefix.Length
                ? n.HierarchyPath[pathPrefix.Length..]
                : string.Empty;
            var firstSeg = rel.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            var leg = firstSeg is not null && legByGatewayId.TryGetValue(firstSeg, out var side)
                ? side.ToString()
                : string.Empty;
            legMap[n.MemberId]   = leg;
            levelMap[n.MemberId] = SegmentCount(n.HierarchyPath) - rootDepth;
        }

        // ─── Profile + filters ───────────────────────────────────────────
        var profileQuery = _db.MemberProfiles.AsNoTracking()
            .Where(m => subtreeIds.Contains(m.MemberId));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            profileQuery = profileQuery.Where(m =>
                m.FirstName.ToLower().Contains(s) ||
                m.LastName.ToLower().Contains(s)  ||
                m.MemberId.ToLower().Contains(s)  ||
                (m.Email != null && m.Email.ToLower().Contains(s)));
        }
        if (from.HasValue) profileQuery = profileQuery.Where(m => m.EnrollDate >= from.Value);
        if (to.HasValue)   profileQuery = profileQuery.Where(m => m.EnrollDate <= to.Value);

        var totalCount = await profileQuery.CountAsync(ct);
        var profiles = await profileQuery
            .OrderByDescending(m => m.EnrollDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new
            {
                m.MemberId, m.FirstName, m.LastName, m.Email, m.Phone,
                m.Country, m.EnrollDate, m.SponsorMemberId,
                AccountStatus = m.Status.ToString()
            })
            .ToListAsync(ct);

        var pageIds = profiles.Select(p => p.MemberId).ToList();

        // ─── Sidecar lookups (mirror EnrollmentTeamService.GetMyTeamAsync) ─
        var subscriptions = await _db.MembershipSubscriptions.AsNoTracking()
            .Include(s => s.MembershipLevel)
            .Where(s => pageIds.Contains(s.MemberId)
                     && s.SubscriptionStatus != MembershipStatus.Cancelled)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(ct);
        var subMap = subscriptions.GroupBy(s => s.MemberId).ToDictionary(g => g.Key, g => g.First());

        var rankHistories = await _db.MemberRankHistories.AsNoTracking()
            .Include(r => r.RankDefinition)
            .Where(r => pageIds.Contains(r.MemberId))
            .ToListAsync(ct);
        var currentRankMap = rankHistories.GroupBy(r => r.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.AchievedAt).First());
        var lifetimeRankMap = rankHistories.GroupBy(r => r.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.RankDefinition?.SortOrder ?? 0).First());

        var pageDualNodes = subtreeNodes.Where(n => pageIds.Contains(n.MemberId))
            .ToDictionary(n => n.MemberId);

        var statsMap = await _db.MemberStatistics.AsNoTracking()
            .Where(s => pageIds.Contains(s.MemberId))
            .ToDictionaryAsync(s => s.MemberId, ct);

        var resolveIds = profiles.Where(p => p.SponsorMemberId != null)
            .Select(p => p.SponsorMemberId!)
            .Union(pageDualNodes.Values.Where(d => d.ParentMemberId != null).Select(d => d.ParentMemberId!))
            .Distinct().ToList();
        var nameMap = await _db.MemberProfiles.AsNoTracking()
            .Where(m => resolveIds.Contains(m.MemberId))
            .Select(m => new { m.MemberId, FullName = m.FirstName + " " + m.LastName })
            .ToDictionaryAsync(m => m.MemberId, m => m.FullName, ct);

        var allRanks = await _db.RankDefinitions.AsNoTracking()
            .Include(r => r.Requirements).OrderBy(r => r.SortOrder).ToListAsync(ct);

        var items = profiles.Select(p =>
        {
            subMap.TryGetValue(p.MemberId, out var sub);
            currentRankMap.TryGetValue(p.MemberId, out var cr);
            lifetimeRankMap.TryGetValue(p.MemberId, out var lr);
            pageDualNodes.TryGetValue(p.MemberId, out var dual);
            statsMap.TryGetValue(p.MemberId, out var stat);
            nameMap.TryGetValue(p.SponsorMemberId ?? "", out var sponsorName);
            nameMap.TryGetValue(dual?.ParentMemberId ?? "", out var uplineName);

            var currentSortOrder = cr?.RankDefinition?.SortOrder ?? 0;
            var nextRank = allRanks.FirstOrDefault(r => r.SortOrder > currentSortOrder);
            int pct = 0;
            if (nextRank is not null)
            {
                var req = nextRank.Requirements.OrderBy(r => r.LevelNo).FirstOrDefault();
                if (req is not null && req.TeamPoints > 0)
                    pct = Math.Min(100, (int)((stat?.DualTeamPoints ?? 0) * 100.0 / req.TeamPoints));
            }
            else if (cr is not null) pct = 100;

            return new DualTeamMyTeamMemberView
            {
                MemberId             = p.MemberId,
                FullName             = $"{p.FirstName} {p.LastName}",
                Email                = p.Email,
                Phone                = p.Phone,
                Country              = p.Country,
                Level                = levelMap.GetValueOrDefault(p.MemberId),
                Leg                  = legMap.GetValueOrDefault(p.MemberId, string.Empty),
                EnrollDate           = p.EnrollDate,
                SponsorMemberId      = p.SponsorMemberId,
                SponsorFullName      = sponsorName,
                DualUplineMemberId   = dual?.ParentMemberId,
                DualUplineFullName   = uplineName,
                AccountStatus        = p.AccountStatus,
                MembershipStatus     = sub?.SubscriptionStatus.ToString() ?? "None",
                IsQualified          = sub?.SubscriptionStatus == MembershipStatus.Active,
                MembershipLevelName  = sub?.MembershipLevel?.Name,
                CurrentRankName      = cr?.RankDefinition?.Name,
                RankDate             = cr?.AchievedAt,
                LifetimeRankName     = lr?.RankDefinition?.Name,
                NextRankPercent      = pct,
                QualificationPoints  = stat?.PersonalPoints   ?? 0,
                EnrollmentTeamPoints = stat?.EnrollmentPoints ?? 0,
                LeftTeamPoints       = dual?.LeftLegPoints  ?? 0,
                RightTeamPoints      = dual?.RightLegPoints ?? 0
            };
        }).ToList();

        return new PagedResult<DualTeamMyTeamMemberView>
        {
            Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize
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
