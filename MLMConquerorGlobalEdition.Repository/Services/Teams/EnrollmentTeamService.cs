using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <inheritdoc />
public class EnrollmentTeamService : IEnrollmentTeamService
{
    private readonly AppDbContext _db;

    public EnrollmentTeamService(AppDbContext db) => _db = db;

    // ─── My Team ───────────────────────────────────────────────────────────
    public async Task<PagedResult<EnrollmentMyTeamMemberView>> GetMyTeamAsync(
        string memberId, int page, int pageSize, string? search,
        DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var myNode = await _db.GenealogyTree.AsNoTracking()
            .FirstOrDefaultAsync(g => g.MemberId == memberId, ct);
        if (myNode is null) return new PagedResult<EnrollmentMyTeamMemberView>();

        var pathPrefix = myNode.HierarchyPath;
        var rootLevel  = myNode.Level;

        // Exclude the viewer themselves: their own HierarchyPath naturally
        // starts with `pathPrefix`, so without this filter they show up at
        // their own absolute genealogy level. The "My Team" view is for
        // descendants only.
        var downlineNodes = await _db.GenealogyTree.AsNoTracking()
            .Where(g => g.HierarchyPath.StartsWith(pathPrefix) && g.MemberId != memberId)
            .Select(g => new { g.MemberId, g.Level })
            .ToListAsync(ct);

        var downlineIds = downlineNodes.Select(x => x.MemberId).ToList();
        // Levels are stored as absolute depth in the genealogy tree; rebase
        // them so the viewer's direct downline appears as Level 1.
        var levelMap    = downlineNodes.ToDictionary(x => x.MemberId, x => x.Level - rootLevel);
        if (!downlineIds.Any()) return new PagedResult<EnrollmentMyTeamMemberView>();

        var profileQuery = _db.MemberProfiles.AsNoTracking()
            .Where(m => downlineIds.Contains(m.MemberId));

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
        var currentRankMap  = rankHistories.GroupBy(r => r.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.AchievedAt).First());
        var lifetimeRankMap = rankHistories.GroupBy(r => r.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.RankDefinition?.SortOrder ?? 0).First());

        var dualNodes = await _db.DualTeamTree.AsNoTracking()
            .Where(d => pageIds.Contains(d.MemberId)).ToListAsync(ct);
        var dualMap = dualNodes.ToDictionary(d => d.MemberId);

        var statsMap = await _db.MemberStatistics.AsNoTracking()
            .Where(s => pageIds.Contains(s.MemberId))
            .ToDictionaryAsync(s => s.MemberId, ct);

        var lastPayments = await _db.PaymentHistories.AsNoTracking()
            .Where(p => pageIds.Contains(p.MemberId)
                     && p.TransactionStatus == PaymentHistoryTransactionStatus.Captured)
            .GroupBy(p => p.MemberId)
            .Select(g => new { MemberId = g.Key, LastDate = g.Max(p => p.ProcessedAt) })
            .ToListAsync(ct);
        var lastPaymentMap = lastPayments.ToDictionary(x => x.MemberId, x => x.LastDate);

        var resolveIds = profiles.Where(p => p.SponsorMemberId != null)
            .Select(p => p.SponsorMemberId!)
            .Union(dualNodes.Where(d => d.ParentMemberId != null).Select(d => d.ParentMemberId!))
            .Distinct().ToList();
        var nameMap = await _db.MemberProfiles.AsNoTracking()
            .Where(m => resolveIds.Contains(m.MemberId))
            .Select(m => new { m.MemberId, FullName = m.FirstName + " " + m.LastName })
            .ToDictionaryAsync(m => m.MemberId, m => m.FullName, ct);

        // Headline rank requirement = lowest LevelNo per rank (works for LevelNo=0 or =SortOrder).
        var allRanks = await _db.RankDefinitions.AsNoTracking()
            .Include(r => r.Requirements).OrderBy(r => r.SortOrder).ToListAsync(ct);

        var items = profiles.Select(p =>
        {
            subMap.TryGetValue(p.MemberId, out var sub);
            currentRankMap.TryGetValue(p.MemberId, out var cr);
            lifetimeRankMap.TryGetValue(p.MemberId, out var lr);
            dualMap.TryGetValue(p.MemberId, out var dual);
            statsMap.TryGetValue(p.MemberId, out var stat);
            lastPaymentMap.TryGetValue(p.MemberId, out var lastPayDate);
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

            return new EnrollmentMyTeamMemberView
            {
                MemberId             = p.MemberId,
                FullName             = $"{p.FirstName} {p.LastName}",
                Email                = p.Email,
                Phone                = p.Phone,
                Country              = p.Country,
                Level                = levelMap.GetValueOrDefault(p.MemberId),
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
                RightTeamPoints      = dual?.RightLegPoints ?? 0,
                SuspensionDate       = sub?.HoldDate,
                CancellationDate     = sub?.CancellationDate,
                LastPaymentDate      = lastPaymentMap.TryGetValue(p.MemberId, out var d) ? d : null
            };
        }).ToList();

        return new PagedResult<EnrollmentMyTeamMemberView>
        {
            Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize
        };
    }

    // ─── Branches ──────────────────────────────────────────────────────────
    public async Task<EnrollmentBranchesView> GetBranchesAsync(
        string memberId, int page, int pageSize, string? search,
        CancellationToken ct = default)
    {
        var directChildIds = await _db.GenealogyTree.AsNoTracking()
            .Where(g => g.ParentMemberId == memberId)
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        if (!directChildIds.Any()) return new EnrollmentBranchesView();

        var profileQuery = _db.MemberProfiles.AsNoTracking()
            .Where(m => directChildIds.Contains(m.MemberId));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            profileQuery = profileQuery.Where(m =>
                m.FirstName.ToLower().Contains(s) ||
                m.LastName.ToLower().Contains(s)  ||
                m.MemberId.ToLower().Contains(s));
        }

        var totalCount     = await profileQuery.CountAsync(ct);
        var allFilteredIds = await profileQuery.Select(m => m.MemberId).ToListAsync(ct);

        var profiles = await profileQuery
            .OrderBy(m => m.FirstName).ThenBy(m => m.LastName)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new { m.MemberId, m.FirstName, m.LastName })
            .ToListAsync(ct);

        var allStats = await _db.MemberStatistics.AsNoTracking()
            .Where(s => allFilteredIds.Contains(s.MemberId))
            .Select(s => new { s.MemberId, s.EnrollmentPoints })
            .ToListAsync(ct);
        var allStatsMap = allStats.ToDictionary(s => s.MemberId, s => s.EnrollmentPoints);

        var currentMemberRank = await _db.MemberRankHistories.AsNoTracking()
            .Include(r => r.RankDefinition)
            .Where(r => r.MemberId == memberId)
            .OrderByDescending(r => r.AchievedAt)
            .FirstOrDefaultAsync(ct);
        var currentRankSortOrder = currentMemberRank?.RankDefinition?.SortOrder ?? 0;

        var allRanks = await _db.RankDefinitions.AsNoTracking()
            .Include(r => r.Requirements).OrderBy(r => r.SortOrder).ToListAsync(ct);

        var currentRankDef = allRanks.FirstOrDefault(r => r.SortOrder == currentRankSortOrder);
        var nextRankDef    = allRanks.FirstOrDefault(r => r.SortOrder > currentRankSortOrder);

        int CalcCap(Domain.Entities.Rank.RankDefinition? rankDef)
        {
            if (rankDef is null) return 0;
            // Use the lowest-LevelNo requirement (works for both LevelNo=0 and =SortOrder).
            var req = rankDef.Requirements.OrderBy(r => r.LevelNo).FirstOrDefault();
            if (req is null || req.TeamPoints <= 0) return 0;
            return (int)Math.Round(req.MaxEnrollmentTeamPointsPerBranch * req.TeamPoints);
        }

        var currentCap = CalcCap(currentRankDef);
        var nextCap    = nextRankDef is not null ? CalcCap(nextRankDef) : currentCap;

        var pageIds   = profiles.Select(p => p.MemberId).ToList();
        var pageStats = await _db.MemberStatistics.AsNoTracking()
            .Where(s => pageIds.Contains(s.MemberId))
            .Select(s => new { s.MemberId, s.EnrollmentPoints })
            .ToListAsync(ct);
        var pageStatsMap = pageStats.ToDictionary(s => s.MemberId, s => s.EnrollmentPoints);

        var totalPoints          = allStatsMap.Values.Sum();
        var totalEligibleCurrent = allFilteredIds.Sum(id =>
        {
            var pts = allStatsMap.TryGetValue(id, out var p) ? p : 0;
            return currentCap > 0 ? Math.Min(pts, currentCap) : pts;
        });
        var totalEligibleNext = allFilteredIds.Sum(id =>
        {
            var pts = allStatsMap.TryGetValue(id, out var p) ? p : 0;
            return nextCap > 0 ? Math.Min(pts, nextCap) : pts;
        });

        var items = profiles.Select(p =>
        {
            var raw   = pageStatsMap.TryGetValue(p.MemberId, out var sp) ? sp : 0;
            var eligC = currentCap > 0 ? Math.Min(raw, currentCap) : raw;
            var eligN = nextCap    > 0 ? Math.Min(raw, nextCap)    : raw;
            var pctC  = currentCap > 0 ? Math.Min(100, eligC * 100 / currentCap) : (raw > 0 ? 100 : 0);
            var pctN  = nextCap    > 0 ? Math.Min(100, eligN * 100 / nextCap)    : (raw > 0 ? 100 : 0);
            return new BranchItemView
            {
                MemberId            = p.MemberId,
                FullName            = $"{p.FirstName} {p.LastName}",
                TotalPoints         = raw,
                EligibleCurrentRank = eligC,
                EligibleNextRank    = eligN,
                EligibleCurrentPct  = pctC,
                EligibleNextPct     = pctN
            };
        }).ToList();

        return new EnrollmentBranchesView
        {
            TotalPoints              = totalPoints,
            TotalEligibleCurrentRank = totalEligibleCurrent,
            TotalEligibleNextRank    = totalEligibleNext,
            Branches = new PagedResult<BranchItemView>
            {
                Items      = items,
                TotalCount = totalCount,
                Page       = page,
                PageSize   = pageSize
            }
        };
    }

    // ─── Branch Detail ─────────────────────────────────────────────────────
    public async Task<BranchDetailView?> GetBranchDetailAsync(
        string branchMemberId, CancellationToken ct = default)
    {
        var branchNode = await _db.GenealogyTree.AsNoTracking()
            .FirstOrDefaultAsync(g => g.MemberId == branchMemberId, ct);
        if (branchNode is null) return null;

        var branchProfile = await _db.MemberProfiles.AsNoTracking()
            .Where(m => m.MemberId == branchMemberId)
            .Select(m => new { m.FirstName, m.LastName })
            .FirstOrDefaultAsync(ct);

        var branchStats = await _db.MemberStatistics.AsNoTracking()
            .Where(s => s.MemberId == branchMemberId)
            .Select(s => s.EnrollmentPoints)
            .FirstOrDefaultAsync(ct);

        var pathPrefix  = branchNode.HierarchyPath;
        var branchLevel = branchNode.Level;

        var downlineNodes = await _db.GenealogyTree.AsNoTracking()
            .Where(g => g.HierarchyPath.StartsWith(pathPrefix) && g.MemberId != branchMemberId)
            .Select(g => new { g.MemberId, g.Level })
            .ToListAsync(ct);

        var downlineIds = downlineNodes.Select(x => x.MemberId).ToList();
        var levelMap    = downlineNodes.ToDictionary(x => x.MemberId, x => x.Level - branchLevel);

        var branchName = branchProfile is not null
            ? $"{branchProfile.FirstName} {branchProfile.LastName}"
            : branchMemberId;

        if (!downlineIds.Any())
            return new BranchDetailView
            {
                BranchMemberId   = branchMemberId,
                BranchMemberName = branchName,
                TotalPoints      = branchStats
            };

        var profiles = await _db.MemberProfiles.AsNoTracking()
            .Where(m => downlineIds.Contains(m.MemberId))
            .Select(m => new { m.MemberId, m.FirstName, m.LastName, m.MemberType, AccountStatus = m.Status.ToString() })
            .ToListAsync(ct);

        var allIds = profiles.Select(p => p.MemberId).ToList();

        var subs = await _db.MembershipSubscriptions.AsNoTracking()
            .Include(s => s.MembershipLevel)
            .Where(s => allIds.Contains(s.MemberId) && s.SubscriptionStatus != MembershipStatus.Cancelled)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(ct);
        var subMap = subs.GroupBy(s => s.MemberId).ToDictionary(g => g.Key, g => g.First());

        var stats = await _db.MemberStatistics.AsNoTracking()
            .Where(s => allIds.Contains(s.MemberId))
            .Select(s => new { s.MemberId, s.EnrollmentPoints })
            .ToListAsync(ct);
        var statsMap = stats.ToDictionary(s => s.MemberId, s => s.EnrollmentPoints);

        var ambassadors = profiles
            .Where(p => p.MemberType == MemberType.Ambassador)
            .OrderBy(p => levelMap.TryGetValue(p.MemberId, out var l) ? l : 0)
            .ThenBy(p => p.FirstName)
            .Select((p, idx) =>
            {
                subMap.TryGetValue(p.MemberId, out var sub);
                return new BranchAmbassadorRow
                {
                    SeqNo               = idx + 1,
                    Level               = levelMap.TryGetValue(p.MemberId, out var lv) ? lv : 0,
                    FullName            = $"{p.FirstName} {p.LastName}",
                    AccountStatus       = p.AccountStatus,
                    MembershipStatus    = sub?.SubscriptionStatus.ToString() ?? "None",
                    IsQualified         = sub?.SubscriptionStatus == MembershipStatus.Active,
                    MembershipLevelName = sub?.MembershipLevel?.Name,
                    EnrollmentPoints    = statsMap.TryGetValue(p.MemberId, out var pts) ? pts : 0
                };
            }).ToList();

        var customers = profiles
            .Where(p => p.MemberType == MemberType.ExternalMember)
            .OrderBy(p => levelMap.TryGetValue(p.MemberId, out var l) ? l : 0)
            .ThenBy(p => p.FirstName)
            .Select((p, idx) =>
            {
                subMap.TryGetValue(p.MemberId, out var sub);
                return new BranchCustomerRow
                {
                    SeqNo               = idx + 1,
                    Level               = levelMap.TryGetValue(p.MemberId, out var lv) ? lv : 0,
                    FullName            = $"{p.FirstName} {p.LastName}",
                    MembershipStatus    = sub?.SubscriptionStatus.ToString() ?? "None",
                    MembershipLevelName = sub?.MembershipLevel?.Name,
                    EnrollmentPoints    = statsMap.TryGetValue(p.MemberId, out var pts) ? pts : 0
                };
            }).ToList();

        return new BranchDetailView
        {
            BranchMemberId   = branchMemberId,
            BranchMemberName = branchName,
            TotalPoints      = branchStats,
            Ambassadors      = ambassadors,
            Customers        = customers
        };
    }

    // ─── Customers ─────────────────────────────────────────────────────────
    public async Task<PagedResult<EnrollmentCustomerView>> GetCustomersAsync(
        string memberId, int page, int pageSize, string? search,
        CancellationToken ct = default)
    {
        var myNode = await _db.GenealogyTree.AsNoTracking()
            .FirstOrDefaultAsync(g => g.MemberId == memberId, ct);
        if (myNode is null) return new PagedResult<EnrollmentCustomerView>();

        var pathPrefix = myNode.HierarchyPath;
        var downlineIds = await _db.GenealogyTree.AsNoTracking()
            .Where(g => g.HierarchyPath.StartsWith(pathPrefix))
            .Select(g => g.MemberId).ToListAsync(ct);
        if (!downlineIds.Any()) return new PagedResult<EnrollmentCustomerView>();

        var query = _db.MemberProfiles.AsNoTracking()
            .Where(m => downlineIds.Contains(m.MemberId)
                     && m.MemberType == MemberType.ExternalMember);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(m =>
                m.FirstName.ToLower().Contains(s) ||
                m.LastName.ToLower().Contains(s)  ||
                m.MemberId.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync(ct);
        var profiles = await query.OrderByDescending(m => m.EnrollDate)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var pageIds = profiles.Select(p => p.MemberId).ToList();

        var subs = await _db.MembershipSubscriptions.AsNoTracking()
            .Include(s => s.MembershipLevel)
            .Where(s => pageIds.Contains(s.MemberId))
            .OrderByDescending(s => s.StartDate).ToListAsync(ct);
        var subMap = subs.GroupBy(s => s.MemberId).ToDictionary(g => g.Key, g => g.First());

        var statsMap = await _db.MemberStatistics.AsNoTracking()
            .Where(s => pageIds.Contains(s.MemberId))
            .ToDictionaryAsync(s => s.MemberId, ct);

        var sponsorIds = profiles.Where(p => p.SponsorMemberId != null)
            .Select(p => p.SponsorMemberId!).Distinct().ToList();
        var nameMap = await _db.MemberProfiles.AsNoTracking()
            .Where(m => sponsorIds.Contains(m.MemberId))
            .Select(m => new { m.MemberId, FullName = m.FirstName + " " + m.LastName })
            .ToDictionaryAsync(m => m.MemberId, m => m.FullName, ct);

        var items = profiles.Select(p =>
        {
            subMap.TryGetValue(p.MemberId, out var sub);
            statsMap.TryGetValue(p.MemberId, out var stat);
            nameMap.TryGetValue(p.SponsorMemberId ?? "", out var sponsorName);
            return new EnrollmentCustomerView
            {
                MemberId         = p.MemberId,
                FullName         = $"{p.FirstName} {p.LastName}",
                Email            = p.Email,
                Phone            = p.Phone,
                Country          = p.Country,
                EnrollDate       = p.EnrollDate,
                SponsorMemberId  = p.SponsorMemberId,
                SponsorFullName  = sponsorName,
                AccountStatus    = p.Status.ToString(),
                MembershipStatus = sub?.SubscriptionStatus.ToString() ?? "None",
                MembershipLevel  = sub?.MembershipLevel?.Name,
                PersonalPoints   = stat?.PersonalPoints ?? 0
            };
        }).ToList();

        return new PagedResult<EnrollmentCustomerView>
        {
            Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize
        };
    }

    // ─── Visualizer Stats ──────────────────────────────────────────────────
    public async Task<EnrollmentVisualizerStatsView> GetVisualizerStatsAsync(
        string memberId, CancellationToken ct = default)
    {
        var pattern = "/" + memberId + "/";
        var downlineMemberIds = await _db.GenealogyTree.AsNoTracking()
            .Where(g => g.HierarchyPath.Contains(pattern))
            .Select(g => g.MemberId).ToListAsync(ct);
        if (!downlineMemberIds.Any()) return new EnrollmentVisualizerStatsView();

        var statusCounts = await _db.MemberProfiles.AsNoTracking()
            .Where(m => downlineMemberIds.Contains(m.MemberId))
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return new EnrollmentVisualizerStatsView
        {
            TotalMembers     = downlineMemberIds.Count,
            TotalQualified   = statusCounts.Where(x => x.Status == MemberAccountStatus.Active).Sum(x => x.Count),
            TotalUnqualified = statusCounts.Where(x => x.Status == MemberAccountStatus.Inactive
                                                    || x.Status == MemberAccountStatus.Suspended).Sum(x => x.Count),
            TotalCancelled   = statusCounts.Where(x => x.Status == MemberAccountStatus.Terminated
                                                    || x.Status == MemberAccountStatus.Pending).Sum(x => x.Count)
        };
    }

    // ─── Visualizer Children ───────────────────────────────────────────────
    public async Task<List<EnrollmentVisualizerChildView>> GetVisualizerChildrenAsync(
        string parentMemberId, CancellationToken ct = default)
    {
        var childIds = await _db.GenealogyTree.AsNoTracking()
            .Where(g => g.ParentMemberId == parentMemberId)
            .Select(g => g.MemberId)
            .ToListAsync(ct);
        if (!childIds.Any()) return new List<EnrollmentVisualizerChildView>();

        var profiles = await _db.MemberProfiles.AsNoTracking()
            .Where(m => childIds.Contains(m.MemberId))
            .Select(m => new { m.MemberId, m.FirstName, m.LastName, m.Status })
            .ToListAsync(ct);

        var stats = await _db.MemberStatistics.AsNoTracking()
            .Where(s => childIds.Contains(s.MemberId))
            .Select(s => new { s.MemberId, s.EnrollmentPoints })
            .ToDictionaryAsync(s => s.MemberId, s => s.EnrollmentPoints, ct);

        var hasChildrenSet = (await _db.GenealogyTree.AsNoTracking()
            .Where(g => childIds.Contains(g.ParentMemberId!))
            .Select(g => g.ParentMemberId!)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();

        return profiles
            .OrderBy(p => p.FirstName).ThenBy(p => p.LastName)
            .Select(p => new EnrollmentVisualizerChildView
            {
                MemberId    = p.MemberId,
                FullName    = $"{p.FirstName} {p.LastName}".Trim(),
                StatusCode  = p.Status switch
                {
                    MemberAccountStatus.Active => "Q",
                    MemberAccountStatus.Inactive or MemberAccountStatus.Suspended => "U",
                    _ => "C"
                },
                Points      = stats.TryGetValue(p.MemberId, out var pts) ? pts : 0,
                HasChildren = hasChildrenSet.Contains(p.MemberId)
            }).ToList();
    }
}
