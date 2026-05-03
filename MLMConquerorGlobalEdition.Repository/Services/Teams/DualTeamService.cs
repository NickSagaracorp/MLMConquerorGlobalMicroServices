using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <inheritdoc />
public class DualTeamService : IDualTeamService
{
    private readonly AppDbContext _db;

    public DualTeamService(AppDbContext db) => _db = db;

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
}
