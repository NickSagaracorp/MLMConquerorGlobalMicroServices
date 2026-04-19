using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Enrollment team data for a specific member — used by the Admin Member Profile page.
/// Routes: /api/v1/admin/members/{memberId}/team/enrollment/*
/// </summary>
[ApiController]
[Route("api/v1/admin/members/{memberId}/team/enrollment")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminMemberEnrollmentController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminMemberEnrollmentController(AppDbContext db) => _db = db;

    // ─── My Team ──────────────────────────────────────────────────────────────
    /// <summary>
    /// GET /api/v1/admin/members/{memberId}/team/enrollment/my-team
    /// All downline members in the enrollment tree for the given member.
    /// </summary>
    [HttpGet("my-team")]
    public async Task<IActionResult> GetMyTeam(
        string memberId,
        [FromQuery] int      page     = 1,
        [FromQuery] int      pageSize = 20,
        [FromQuery] string?  search   = null,
        [FromQuery] DateTime? from    = null,
        [FromQuery] DateTime? to      = null,
        CancellationToken ct = default)
    {
        // 1. HierarchyPath of the target member
        var myNode = await _db.GenealogyTree.AsNoTracking()
            .FirstOrDefaultAsync(g => g.MemberId == memberId, ct);

        if (myNode is null)
            return Ok(ApiResponse<PagedResult<EnrollmentMemberDto>>.Ok(new PagedResult<EnrollmentMemberDto>()));

        var pathPrefix = myNode.HierarchyPath;

        var downlineNodes = await _db.GenealogyTree.AsNoTracking()
            .Where(g => g.HierarchyPath.StartsWith(pathPrefix))
            .Select(g => new { g.MemberId, g.Level })
            .ToListAsync(ct);

        var downlineIds = downlineNodes.Select(x => x.MemberId).ToList();
        var levelMap    = downlineNodes.ToDictionary(x => x.MemberId, x => x.Level);

        if (!downlineIds.Any())
            return Ok(ApiResponse<PagedResult<EnrollmentMemberDto>>.Ok(new PagedResult<EnrollmentMemberDto>()));

        // 2. Filter profiles
        var profileQuery = _db.MemberProfiles.AsNoTracking()
            .Where(m => downlineIds.Contains(m.MemberId));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            profileQuery = profileQuery.Where(m =>
                m.FirstName.ToLower().Contains(s) ||
                m.LastName.ToLower().Contains(s)  ||
                m.MemberId.ToLower().Contains(s)  ||
                m.Email.ToLower().Contains(s));
        }
        if (from.HasValue) profileQuery = profileQuery.Where(m => m.EnrollDate >= from.Value);
        if (to.HasValue)   profileQuery = profileQuery.Where(m => m.EnrollDate <= to.Value);

        var totalCount = await profileQuery.CountAsync(ct);
        var profiles   = await profileQuery
            .OrderByDescending(m => m.EnrollDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new { m.MemberId, m.FirstName, m.LastName, m.Email, m.Phone,
                               m.Country, m.EnrollDate, m.SponsorMemberId,
                               AccountStatus = m.Status.ToString() })
            .ToListAsync(ct);

        var pageIds = profiles.Select(p => p.MemberId).ToList();

        // 3. Memberships
        var subs = await _db.MembershipSubscriptions.AsNoTracking()
            .Include(s => s.MembershipLevel)
            .Where(s => pageIds.Contains(s.MemberId))
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(ct);
        var subMap = subs.GroupBy(s => s.MemberId).ToDictionary(g => g.Key, g => g.First());

        // 4. Ranks
        var ranks = await _db.MemberRankHistories.AsNoTracking()
            .Include(r => r.RankDefinition)
            .Where(r => pageIds.Contains(r.MemberId))
            .ToListAsync(ct);
        var currentRankMap  = ranks.GroupBy(r => r.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.AchievedAt).First());
        var lifetimeRankMap = ranks.GroupBy(r => r.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.RankDefinition?.SortOrder ?? 0).First());

        // 5. Dual tree (for left/right points + upline)
        var dualNodes = await _db.DualTeamTree.AsNoTracking()
            .Where(d => pageIds.Contains(d.MemberId)).ToListAsync(ct);
        var dualMap = dualNodes.ToDictionary(d => d.MemberId);

        // 6. Stats
        var statsMap = await _db.MemberStatistics.AsNoTracking()
            .Where(s => pageIds.Contains(s.MemberId))
            .ToDictionaryAsync(s => s.MemberId, ct);

        // 7. Name resolution for sponsors and dual uplines
        var resolveIds = profiles.Where(p => p.SponsorMemberId != null).Select(p => p.SponsorMemberId!)
            .Union(dualNodes.Where(d => d.ParentMemberId != null).Select(d => d.ParentMemberId!))
            .Distinct().ToList();
        var nameMap = await _db.MemberProfiles.AsNoTracking()
            .Where(m => resolveIds.Contains(m.MemberId))
            .Select(m => new { m.MemberId, FullName = m.FirstName + " " + m.LastName })
            .ToDictionaryAsync(m => m.MemberId, m => m.FullName, ct);

        // 8. Next rank reference
        var allRanks = await _db.RankDefinitions.AsNoTracking()
            .Include(r => r.Requirements).OrderBy(r => r.SortOrder).ToListAsync(ct);

        var items = profiles.Select(p =>
        {
            subMap.TryGetValue(p.MemberId, out var sub);
            currentRankMap.TryGetValue(p.MemberId, out var cr);
            lifetimeRankMap.TryGetValue(p.MemberId, out var lr);
            dualMap.TryGetValue(p.MemberId, out var dual);
            statsMap.TryGetValue(p.MemberId, out var stat);
            nameMap.TryGetValue(p.SponsorMemberId ?? "", out var sponsorName);
            nameMap.TryGetValue(dual?.ParentMemberId ?? "", out var uplineName);

            var currentSortOrder = cr?.RankDefinition?.SortOrder ?? 0;
            var nextRank = allRanks.FirstOrDefault(r => r.SortOrder > currentSortOrder);
            int pct = 0;
            if (nextRank is not null)
            {
                var req = nextRank.Requirements.FirstOrDefault(r => r.LevelNo == 0);
                if (req is not null && req.TeamPoints > 0)
                    pct = Math.Min(100, (int)((stat?.DualTeamPoints ?? 0) * 100.0 / req.TeamPoints));
            }
            else if (cr is not null) pct = 100;

            return new EnrollmentMemberDto
            {
                MemberId            = p.MemberId,
                FullName            = $"{p.FirstName} {p.LastName}",
                Email               = p.Email,
                Phone               = p.Phone,
                Country             = p.Country,
                Level               = levelMap.GetValueOrDefault(p.MemberId),
                EnrollDate          = p.EnrollDate,
                SponsorMemberId     = p.SponsorMemberId,
                SponsorFullName     = sponsorName,
                DualUplineMemberId  = dual?.ParentMemberId,
                DualUplineFullName  = uplineName,
                AccountStatus       = p.AccountStatus,
                MembershipStatus    = sub?.SubscriptionStatus.ToString() ?? "None",
                IsQualified         = sub?.SubscriptionStatus == MembershipStatus.Active,
                MembershipLevelName = sub?.MembershipLevel?.Name,
                CurrentRankName     = cr?.RankDefinition?.Name,
                RankDate            = cr?.AchievedAt,
                LifetimeRankName    = lr?.RankDefinition?.Name,
                NextRankPercent     = pct,
                QualificationPoints    = stat?.PersonalPoints ?? 0,
                EnrollmentTeamPoints   = stat?.EnrollmentPoints ?? 0,
                LeftTeamPoints      = dual?.LeftLegPoints ?? 0,
                RightTeamPoints     = dual?.RightLegPoints ?? 0
            };
        }).ToList();

        return Ok(ApiResponse<PagedResult<EnrollmentMemberDto>>.Ok(new PagedResult<EnrollmentMemberDto>
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize
        }));
    }

    // ─── Branches — matches EnrollmentTeamBranches shared component format ───
    /// <summary>
    /// GET /api/v1/admin/members/{memberId}/team/enrollment/branches
    /// Returns rank-eligible branch stats in the same shape as BizCenter.
    /// </summary>
    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches(
        string memberId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var directChildIds = await _db.GenealogyTree.AsNoTracking()
            .Where(g => g.ParentMemberId == memberId)
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        if (!directChildIds.Any())
            return Ok(ApiResponse<BranchesResultDto>.Ok(new BranchesResultDto()));

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

        var totalCount      = await profileQuery.CountAsync(ct);
        var allFilteredIds  = await profileQuery.Select(m => m.MemberId).ToListAsync(ct);

        var profiles = await profileQuery
            .OrderBy(m => m.FirstName).ThenBy(m => m.LastName)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new { m.MemberId, m.FirstName, m.LastName })
            .ToListAsync(ct);

        // All stats for summary totals
        var allStats = await _db.MemberStatistics.AsNoTracking()
            .Where(s => allFilteredIds.Contains(s.MemberId))
            .Select(s => new { s.MemberId, s.EnrollmentPoints })
            .ToListAsync(ct);
        var allStatsMap = allStats.ToDictionary(s => s.MemberId, s => s.EnrollmentPoints);

        // Rank caps from target member's current/next rank
        var currentMemberRank = await _db.MemberRankHistories.AsNoTracking()
            .Include(r => r.RankDefinition)
            .Where(r => r.MemberId == memberId)
            .OrderByDescending(r => r.AchievedAt)
            .FirstOrDefaultAsync(ct);

        var currentRankSortOrder = currentMemberRank?.RankDefinition?.SortOrder ?? 0;

        var allRanks = await _db.RankDefinitions.AsNoTracking()
            .Include(r => r.Requirements)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        var currentRankDef = allRanks.FirstOrDefault(r => r.SortOrder == currentRankSortOrder);
        var nextRankDef    = allRanks.FirstOrDefault(r => r.SortOrder > currentRankSortOrder);

        int CalcCap(Domain.Entities.Rank.RankDefinition? rankDef)
        {
            if (rankDef is null) return 0;
            var req = rankDef.Requirements.FirstOrDefault(r => r.LevelNo == 0);
            if (req is null || req.TeamPoints <= 0) return 0;
            return (int)Math.Round(req.MaxEnrollmentTeamPointsPerBranch * req.TeamPoints);
        }

        var currentCap = CalcCap(currentRankDef);
        var nextCap    = nextRankDef is not null ? CalcCap(nextRankDef) : currentCap;

        // Page stats
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
            var raw     = pageStatsMap.TryGetValue(p.MemberId, out var sp) ? sp : 0;
            var eligC   = currentCap > 0 ? Math.Min(raw, currentCap) : raw;
            var eligN   = nextCap    > 0 ? Math.Min(raw, nextCap)    : raw;
            var pctC    = currentCap > 0 ? Math.Min(100, eligC * 100 / currentCap) : (raw > 0 ? 100 : 0);
            var pctN    = nextCap    > 0 ? Math.Min(100, eligN * 100 / nextCap)    : (raw > 0 ? 100 : 0);
            return new BranchItemDto
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

        return Ok(ApiResponse<BranchesResultDto>.Ok(new BranchesResultDto
        {
            TotalPoints              = totalPoints,
            TotalEligibleCurrentRank = totalEligibleCurrent,
            TotalEligibleNextRank    = totalEligibleNext,
            Branches = new BranchPagedData
            {
                Items      = items,
                TotalCount = totalCount,
                Page       = page,
                PageSize   = pageSize
            }
        }));
    }

    // ─── Branch Detail ────────────────────────────────────────────────────────
    /// <summary>
    /// GET /api/v1/admin/members/{memberId}/team/enrollment/branches/{branchMemberId}/detail
    /// Full downline of a branch (ambassadors + customers) — no ownership check in admin.
    /// </summary>
    [HttpGet("branches/{branchMemberId}/detail")]
    public async Task<IActionResult> GetBranchDetail(
        string memberId, string branchMemberId, CancellationToken ct = default)
    {
        var branchNode = await _db.GenealogyTree.AsNoTracking()
            .FirstOrDefaultAsync(g => g.MemberId == branchMemberId, ct);

        if (branchNode is null)
            return NotFound(ApiResponse<BranchDetailDto>.Fail("NOT_FOUND", "Branch not found."));

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

        if (!downlineIds.Any())
            return Ok(ApiResponse<BranchDetailDto>.Ok(new BranchDetailDto
            {
                BranchMemberId   = branchMemberId,
                BranchMemberName = branchProfile is not null ? $"{branchProfile.FirstName} {branchProfile.LastName}" : branchMemberId,
                TotalPoints      = branchStats
            }));

        var profiles = await _db.MemberProfiles.AsNoTracking()
            .Where(m => downlineIds.Contains(m.MemberId))
            .Select(m => new { m.MemberId, m.FirstName, m.LastName, m.MemberType, AccountStatus = m.Status.ToString() })
            .ToListAsync(ct);

        var allIds = profiles.Select(p => p.MemberId).ToList();

        var subscriptions = await _db.MembershipSubscriptions.AsNoTracking()
            .Include(s => s.MembershipLevel)
            .Where(s => allIds.Contains(s.MemberId) && s.SubscriptionStatus != MembershipStatus.Cancelled)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(ct);
        var subMap = subscriptions.GroupBy(s => s.MemberId).ToDictionary(g => g.Key, g => g.First());

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
                return new BranchAmbassadorItem
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
                return new BranchCustomerItem
                {
                    SeqNo               = idx + 1,
                    Level               = levelMap.TryGetValue(p.MemberId, out var lv) ? lv : 0,
                    FullName            = $"{p.FirstName} {p.LastName}",
                    MembershipStatus    = sub?.SubscriptionStatus.ToString() ?? "None",
                    MembershipLevelName = sub?.MembershipLevel?.Name,
                    EnrollmentPoints    = statsMap.TryGetValue(p.MemberId, out var pts) ? pts : 0
                };
            }).ToList();

        return Ok(ApiResponse<BranchDetailDto>.Ok(new BranchDetailDto
        {
            BranchMemberId   = branchMemberId,
            BranchMemberName = branchProfile is not null ? $"{branchProfile.FirstName} {branchProfile.LastName}" : branchMemberId,
            TotalPoints      = branchStats,
            Ambassadors      = ambassadors,
            Customers        = customers
        }));
    }

    // ─── Customers ────────────────────────────────────────────────────────────
    /// <summary>
    /// GET /api/v1/admin/members/{memberId}/team/enrollment/customers
    /// ExternalMember type only across the full subtree.
    /// </summary>
    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomers(
        string memberId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var myNode = await _db.GenealogyTree.AsNoTracking()
            .FirstOrDefaultAsync(g => g.MemberId == memberId, ct);

        if (myNode is null)
            return Ok(ApiResponse<PagedResult<CustomerMemberDto>>.Ok(new PagedResult<CustomerMemberDto>()));

        var pathPrefix = myNode.HierarchyPath;
        var downlineIds = await _db.GenealogyTree.AsNoTracking()
            .Where(g => g.HierarchyPath.StartsWith(pathPrefix))
            .Select(g => g.MemberId).ToListAsync(ct);

        if (!downlineIds.Any())
            return Ok(ApiResponse<PagedResult<CustomerMemberDto>>.Ok(new PagedResult<CustomerMemberDto>()));

        var query = _db.MemberProfiles.AsNoTracking()
            .Where(m => downlineIds.Contains(m.MemberId) && m.MemberType == MemberType.ExternalMember);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(m =>
                m.FirstName.ToLower().Contains(s) ||
                m.LastName.ToLower().Contains(s)  ||
                m.MemberId.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync(ct);
        var profiles   = await query.OrderByDescending(m => m.EnrollDate)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var subs = await _db.MembershipSubscriptions.AsNoTracking()
            .Include(s => s.MembershipLevel)
            .Where(s => profiles.Select(p => p.MemberId).Contains(s.MemberId))
            .OrderByDescending(s => s.StartDate).ToListAsync(ct);
        var subMap = subs.GroupBy(s => s.MemberId).ToDictionary(g => g.Key, g => g.First());

        var statsMap = await _db.MemberStatistics.AsNoTracking()
            .Where(s => profiles.Select(p => p.MemberId).Contains(s.MemberId))
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
            return new CustomerMemberDto
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

        return Ok(ApiResponse<PagedResult<CustomerMemberDto>>.Ok(new PagedResult<CustomerMemberDto>
        {
            Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize
        }));
    }

    // ─── Visualizer Stats ─────────────────────────────────────────────────────
    /// <summary>
    /// GET /api/v1/admin/members/{memberId}/team/enrollment/visualizer/stats
    /// Downline status counts for the tree visualizer — no auth restriction on subtree.
    /// </summary>
    [HttpGet("visualizer/stats")]
    public async Task<IActionResult> GetVisualizerStats(string memberId, CancellationToken ct = default)
    {
        var pattern = "/" + memberId + "/";

        var downlineMemberIds = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => g.HierarchyPath.Contains(pattern))
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        if (!downlineMemberIds.Any())
            return Ok(ApiResponse<VisualizerStatsDto>.Ok(new VisualizerStatsDto()));

        var statusCounts = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => downlineMemberIds.Contains(m.MemberId))
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var dto = new VisualizerStatsDto
        {
            TotalMembers     = downlineMemberIds.Count,
            TotalQualified   = statusCounts.Where(x => x.Status == MemberAccountStatus.Active).Sum(x => x.Count),
            TotalUnqualified = statusCounts.Where(x => x.Status == MemberAccountStatus.Inactive || x.Status == MemberAccountStatus.Suspended).Sum(x => x.Count),
            TotalCancelled   = statusCounts.Where(x => x.Status == MemberAccountStatus.Terminated || x.Status == MemberAccountStatus.Pending).Sum(x => x.Count)
        };

        return Ok(ApiResponse<VisualizerStatsDto>.Ok(dto));
    }

    // ─── Visualizer Children ──────────────────────────────────────────────────
    /// <summary>
    /// GET /api/v1/admin/members/{memberId}/team/enrollment/visualizer/children/{parentMemberId}
    /// Direct children of parentMemberId for lazy tree expansion — no subtree security check (admin).
    /// </summary>
    [HttpGet("visualizer/children/{parentMemberId}")]
    public async Task<IActionResult> GetVisualizerChildren(
        string memberId, string parentMemberId, CancellationToken ct = default)
    {
        var childIds = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => g.ParentMemberId == parentMemberId)
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        if (!childIds.Any())
            return Ok(ApiResponse<IEnumerable<VisualizerNodeDto>>.Ok(Enumerable.Empty<VisualizerNodeDto>()));

        var profiles = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => childIds.Contains(m.MemberId))
            .Select(m => new { m.MemberId, m.FirstName, m.LastName, m.Status })
            .ToListAsync(ct);

        var stats = await _db.MemberStatistics
            .AsNoTracking()
            .Where(s => childIds.Contains(s.MemberId))
            .Select(s => new { s.MemberId, s.EnrollmentPoints })
            .ToDictionaryAsync(s => s.MemberId, s => s.EnrollmentPoints, ct);

        var hasChildrenSet = new HashSet<string>(
            await _db.GenealogyTree
                .AsNoTracking()
                .Where(g => childIds.Contains(g.ParentMemberId))
                .Select(g => g.ParentMemberId)
                .Distinct()
                .ToListAsync(ct));

        var nodes = profiles
            .OrderBy(p => p.FirstName).ThenBy(p => p.LastName)
            .Select(p => new VisualizerNodeDto
            {
                MemberId    = p.MemberId,
                FullName    = $"{p.FirstName} {p.LastName}".Trim(),
                StatusCode  = p.Status switch
                {
                    MemberAccountStatus.Active                                     => "Q",
                    MemberAccountStatus.Inactive or MemberAccountStatus.Suspended => "U",
                    _                                                              => "C"
                },
                Points      = stats.TryGetValue(p.MemberId, out var pts) ? pts : 0,
                HasChildren = hasChildrenSet.Contains(p.MemberId)
            });

        return Ok(ApiResponse<IEnumerable<VisualizerNodeDto>>.Ok(nodes));
    }

    // ─── DTOs ─────────────────────────────────────────────────────────────────
    public class EnrollmentMemberDto
    {
        public string    MemberId            { get; set; } = string.Empty;
        public string    FullName            { get; set; } = string.Empty;
        public string    Email               { get; set; } = string.Empty;
        public string?   Phone               { get; set; }
        public string    Country             { get; set; } = string.Empty;
        public int       Level               { get; set; }
        public DateTime  EnrollDate          { get; set; }
        public string?   SponsorMemberId     { get; set; }
        public string?   SponsorFullName     { get; set; }
        public string?   DualUplineMemberId  { get; set; }
        public string?   DualUplineFullName  { get; set; }
        public string    AccountStatus       { get; set; } = string.Empty;
        public string    MembershipStatus    { get; set; } = string.Empty;
        public bool      IsQualified         { get; set; }
        public string?   MembershipLevelName { get; set; }
        public string?   CurrentRankName     { get; set; }
        public DateTime? RankDate            { get; set; }
        public string?   LifetimeRankName    { get; set; }
        public int       NextRankPercent     { get; set; }
        public int       QualificationPoints    { get; set; }
        public int       EnrollmentTeamPoints   { get; set; }
        public decimal   LeftTeamPoints         { get; set; }
        public decimal   RightTeamPoints        { get; set; }
    }

    // Branches result (matches EnrollmentTeamBranches shared component)
    public class BranchesResultDto
    {
        public int            TotalPoints              { get; set; }
        public int            TotalEligibleCurrentRank { get; set; }
        public int            TotalEligibleNextRank    { get; set; }
        public BranchPagedData Branches                { get; set; } = new();
    }
    public class BranchPagedData
    {
        public List<BranchItemDto> Items      { get; set; } = new();
        public int                 TotalCount { get; set; }
        public int                 Page       { get; set; }
        public int                 PageSize   { get; set; }
    }
    public class BranchItemDto
    {
        public string MemberId            { get; set; } = string.Empty;
        public string FullName            { get; set; } = string.Empty;
        public int    TotalPoints         { get; set; }
        public int    EligibleCurrentRank { get; set; }
        public int    EligibleNextRank    { get; set; }
        public int    EligibleCurrentPct  { get; set; }
        public int    EligibleNextPct     { get; set; }
    }
    // Branch detail (matches EnrollmentTeamBranches modal)
    public class BranchDetailDto
    {
        public string BranchMemberId    { get; set; } = string.Empty;
        public string BranchMemberName  { get; set; } = string.Empty;
        public int    TotalPoints       { get; set; }
        public List<BranchAmbassadorItem> Ambassadors { get; set; } = new();
        public List<BranchCustomerItem>   Customers   { get; set; } = new();
    }
    public class BranchAmbassadorItem
    {
        public int     SeqNo              { get; set; }
        public int     Level              { get; set; }
        public string  FullName           { get; set; } = string.Empty;
        public string  AccountStatus      { get; set; } = string.Empty;
        public string  MembershipStatus   { get; set; } = string.Empty;
        public bool    IsQualified        { get; set; }
        public string? MembershipLevelName{ get; set; }
        public int     EnrollmentPoints   { get; set; }
    }
    public class BranchCustomerItem
    {
        public int     SeqNo              { get; set; }
        public int     Level              { get; set; }
        public string  FullName           { get; set; } = string.Empty;
        public string  MembershipStatus   { get; set; } = string.Empty;
        public string? MembershipLevelName{ get; set; }
        public int     EnrollmentPoints   { get; set; }
    }

    public class CustomerMemberDto
    {
        public string   MemberId         { get; set; } = string.Empty;
        public string   FullName         { get; set; } = string.Empty;
        public string   Email            { get; set; } = string.Empty;
        public string?  Phone            { get; set; }
        public string   Country          { get; set; } = string.Empty;
        public DateTime EnrollDate       { get; set; }
        public string?  SponsorMemberId  { get; set; }
        public string?  SponsorFullName  { get; set; }
        public string   AccountStatus    { get; set; } = string.Empty;
        public string   MembershipStatus { get; set; } = string.Empty;
        public string?  MembershipLevel  { get; set; }
        public int      PersonalPoints   { get; set; }
    }

    public class VisualizerStatsDto
    {
        public int TotalMembers     { get; set; }
        public int TotalQualified   { get; set; }
        public int TotalUnqualified { get; set; }
        public int TotalCancelled   { get; set; }
    }

    public class VisualizerNodeDto
    {
        public string MemberId    { get; set; } = string.Empty;
        public string FullName    { get; set; } = string.Empty;
        public string StatusCode  { get; set; } = "Q";
        public int    Points      { get; set; }
        public bool   HasChildren { get; set; }
    }
}
