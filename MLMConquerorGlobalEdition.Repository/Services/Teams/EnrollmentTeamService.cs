using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <inheritdoc />
public class EnrollmentTeamService : IEnrollmentTeamService
{
    private readonly AppDbContext                  _db;
    private readonly IRankComputationService       _ranks;
    private readonly IEnrollmentTeamPointsService  _etPoints;

    public EnrollmentTeamService(
        AppDbContext db,
        IRankComputationService ranks,
        IEnrollmentTeamPointsService etPoints)
    {
        _db       = db;
        _ranks    = ranks;
        _etPoints = etPoints;
    }

    /// <summary>
    /// Columns the my-team grid can search/sort/filter AT THE DB — i.e. those present in the
    /// light <see cref="BuildMyTeamQueryable"/> projection. Enriched columns (rank/membership/
    /// sponsor name) are filled per-page only, so grid operations on them are dropped (see
    /// <see cref="SanitizeGridRequest"/>) rather than forcing a 120k-row subquery scan.
    /// </summary>
    private static readonly string[] MyTeamDbFields =
    {
        nameof(EnrollmentMyTeamMemberView.MemberId),
        nameof(EnrollmentMyTeamMemberView.FullName),
        nameof(EnrollmentMyTeamMemberView.Email),
        nameof(EnrollmentMyTeamMemberView.Phone),
        nameof(EnrollmentMyTeamMemberView.Country),
        nameof(EnrollmentMyTeamMemberView.Level),
        nameof(EnrollmentMyTeamMemberView.EnrollDate),
        nameof(EnrollmentMyTeamMemberView.SponsorMemberId),
        nameof(EnrollmentMyTeamMemberView.AccountStatus),
    };

    /// <summary>String DB columns the my-team grid search box matches against.</summary>
    private static readonly string[] MyTeamSearchableFields =
    {
        nameof(EnrollmentMyTeamMemberView.MemberId),
        nameof(EnrollmentMyTeamMemberView.FullName),
        nameof(EnrollmentMyTeamMemberView.Email),
        nameof(EnrollmentMyTeamMemberView.Phone),
        nameof(EnrollmentMyTeamMemberView.Country),
        nameof(EnrollmentMyTeamMemberView.AccountStatus),
    };

    /// <summary>Drop sorts/filters that reference columns not in the DB projection so the grid
    /// query stays a fast indexed seek instead of erroring on a null-constant column.</summary>
    private static void SanitizeGridRequest(GridDataRequest request)
    {
        request.Sorts   = request.Sorts.Where(s => MyTeamDbFields.Contains(s.Field, StringComparer.OrdinalIgnoreCase)).ToList();
        request.Filters = request.Filters.Where(f => MyTeamDbFields.Contains(f.Field, StringComparer.OrdinalIgnoreCase)).ToList();
        if (request.Sorts.Count == 0)
            request.Sorts.Add(new GridSort { Field = nameof(EnrollmentMyTeamMemberView.EnrollDate), Direction = "desc" });
    }

    // ─── My Team ───────────────────────────────────────────────────────────
    public async Task<PagedResult<EnrollmentMyTeamMemberView>> GetMyTeamAsync(
        string memberId, int page, int pageSize, string? search,
        DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var myNode = await _db.GenealogyTree.AsNoTracking()
            .FirstOrDefaultAsync(g => g.MemberId == memberId, ct);
        if (myNode is null) return new PagedResult<EnrollmentMyTeamMemberView>();

        var query = BuildMyTeamQueryable(myNode.HierarchyPath, myNode.Level, memberId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(v =>
                v.FullName.ToLower().Contains(s) ||
                v.MemberId.ToLower().Contains(s) ||
                (v.Email != null && v.Email.ToLower().Contains(s)));
        }
        if (from.HasValue) query = query.Where(v => v.EnrollDate >= from.Value);
        if (to.HasValue)   query = query.Where(v => v.EnrollDate <= to.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(v => v.EnrollDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        await EnrichPageAsync(items, ct);

        return new PagedResult<EnrollmentMyTeamMemberView>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        };
    }

    /// <summary>
    /// Server-side grid read over the viewer's WHOLE enrollment (genealogy)
    /// downline, so search / filter / sort span every page. Computed columns
    /// (Level, NextRankPercent) are resolved in C#, so the pipeline runs
    /// in-memory over the materialized downline (bounded per member).
    /// </summary>
    public async Task<PagedResult<EnrollmentMyTeamMemberView>> GetMyTeamGridAsync(
        string memberId, GridDataRequest request, CancellationToken ct = default)
    {
        var myNode = await _db.GenealogyTree.AsNoTracking()
            .FirstOrDefaultAsync(g => g.MemberId == memberId, ct);
        if (myNode is null) return new PagedResult<EnrollmentMyTeamMemberView>();

        SanitizeGridRequest(request);

        // DB-side search/filter/sort/page over the projected downline query — only the
        // requested page (≤ pageSize rows) is materialized, instead of the whole subtree.
        var query  = BuildMyTeamQueryable(myNode.HierarchyPath, myNode.Level, memberId);
        var result = await query.ToGridResultAsync(request, MyTeamSearchableFields, ct: ct);
        await EnrichPageAsync(result.Items, ct);
        return result;
    }

    /// <summary>
    /// LIGHT DB projection of the viewer's enrollment downline — only directly-translatable
    /// profile columns, so the grid's search · filter · sort · COUNT · page all run fast in SQL
    /// with NO per-row subqueries and NO full-subtree materialization (the previous version
    /// loaded the WHOLE subtree — 120k+ rows × 7 tables via Contains(allIds) — and paged in
    /// memory, taking &gt;60s). The enriched columns (sponsor/upline name, membership, rank,
    /// points, dates, NextRankPercent) are filled for the requested PAGE only by
    /// <see cref="EnrichPageAsync"/>. Search/sort on those enriched columns therefore falls back
    /// to the page (they are null in this projection) — acceptable since the load path uses the
    /// default EnrollDate sort, and enriched-column search at 120k scale needs denormalization.
    /// </summary>
    private IQueryable<EnrollmentMyTeamMemberView> BuildMyTeamQueryable(
        string pathPrefix, int rootLevel, string memberId)
    {
        return
            from g in _db.GenealogyTree.AsNoTracking()
            where g.HierarchyPath.StartsWith(pathPrefix) && g.MemberId != memberId
            join m in _db.MemberProfiles.AsNoTracking() on g.MemberId equals m.MemberId
            select new EnrollmentMyTeamMemberView
            {
                MemberId        = m.MemberId,
                FullName        = m.FirstName + " " + m.LastName,
                Email           = m.Email,
                Phone           = m.Phone,
                Country         = m.Country,
                Level           = g.Level - rootLevel,
                EnrollDate      = m.EnrollDate,
                SponsorMemberId = m.SponsorMemberId,
                AccountStatus   = m.Status.ToString()
            };
    }

    /// <summary>
    /// Fills every enriched column on an already-paged set of rows (≤ pageSize). This is the old
    /// full-subtree enrichment, but scoped to the page's handful of member ids (Contains(~20))
    /// instead of all 120k — so it is cheap regardless of downline size.
    /// </summary>
    private async Task EnrichPageAsync(
        IEnumerable<EnrollmentMyTeamMemberView> items, CancellationToken ct)
    {
        var rows = items as IList<EnrollmentMyTeamMemberView> ?? items.ToList();
        if (rows.Count == 0) return;
        var ids = rows.Select(r => r.MemberId).ToList();

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
        var currentRankMap  = rankHistories.GroupBy(r => r.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.AchievedAt).First());
        var lifetimeRankMap = rankHistories.GroupBy(r => r.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.RankDefinition?.SortOrder ?? 0).First());

        var dualNodes = await _db.DualTeamTree.AsNoTracking()
            .Where(d => ids.Contains(d.MemberId)).ToListAsync(ct);
        var dualMap = dualNodes.ToDictionary(d => d.MemberId);

        var statsMap = await _db.MemberStatistics.AsNoTracking()
            .Where(s => ids.Contains(s.MemberId))
            .ToDictionaryAsync(s => s.MemberId, ct);

        var lastPaymentMap = (await _db.PaymentHistories.AsNoTracking()
            .Where(p => ids.Contains(p.MemberId)
                     && p.TransactionStatus == PaymentHistoryTransactionStatus.Captured)
            .GroupBy(p => p.MemberId)
            .Select(g => new { MemberId = g.Key, LastDate = g.Max(p => p.ProcessedAt) })
            .ToListAsync(ct)).ToDictionary(x => x.MemberId, x => x.LastDate);

        var resolveIds = rows.Where(r => r.SponsorMemberId != null).Select(r => r.SponsorMemberId!)
            .Union(dualNodes.Where(d => d.ParentMemberId != null).Select(d => d.ParentMemberId!))
            .Distinct().ToList();
        var nameMap = await _db.MemberProfiles.AsNoTracking()
            .Where(m => resolveIds.Contains(m.MemberId))
            .Select(m => new { m.MemberId, FullName = m.FirstName + " " + m.LastName })
            .ToDictionaryAsync(m => m.MemberId, m => m.FullName, ct);

        var allRanks = await _db.RankDefinitions.AsNoTracking()
            .Include(r => r.Requirements).OrderBy(r => r.SortOrder).ToListAsync(ct);

        foreach (var row in rows)
        {
            subMap.TryGetValue(row.MemberId, out var sub);
            currentRankMap.TryGetValue(row.MemberId, out var cr);
            lifetimeRankMap.TryGetValue(row.MemberId, out var lr);
            dualMap.TryGetValue(row.MemberId, out var dual);
            statsMap.TryGetValue(row.MemberId, out var stat);
            nameMap.TryGetValue(row.SponsorMemberId ?? "", out var sponsorName);
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

            row.SponsorFullName      = sponsorName;
            row.DualUplineMemberId   = dual?.ParentMemberId;
            row.DualUplineFullName   = uplineName;
            row.MembershipStatus     = sub?.SubscriptionStatus.ToString() ?? "None";
            row.IsQualified          = sub?.SubscriptionStatus == MembershipStatus.Active;
            row.MembershipLevelName  = sub?.MembershipLevel?.Name;
            row.CurrentRankName      = cr?.RankDefinition?.Name;
            row.RankDate             = cr?.AchievedAt;
            row.LifetimeRankName     = lr?.RankDefinition?.Name;
            row.NextRankPercent      = pct;
            row.QualificationPoints  = stat?.PersonalPoints   ?? 0;
            row.EnrollmentTeamPoints = stat?.EnrollmentPoints ?? 0;
            row.LeftTeamPoints       = dual?.LeftLegPoints  ?? 0;
            row.RightTeamPoints      = dual?.RightLegPoints ?? 0;
            row.SuspensionDate       = sub?.HoldDate;
            row.CancellationDate     = sub?.CancellationDate;
            row.LastPaymentDate      = lastPaymentMap.TryGetValue(row.MemberId, out var d) ? d : null;
        }
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

        // Single source of truth: fetch branch points once via the dedicated service
        // (reads MemberStatisticEntity.EnrollmentPoints per direct child, same data as
        // the previous double-query approach but centralised and cache-friendly).
        var branchPointsList = await _etPoints.GetEnrollmentBranchPointsAsync(memberId, ct);
        var allStatsMap = branchPointsList.ToDictionary(b => b.ChildMemberId, b => b.BranchPoints);

        // Live current rank — same source the Profile and Residuals widgets
        // consume. Pulling from MemberRankHistories.AchievedAt would freeze the
        // cap at whatever rank was last manually awarded and silently break
        // the donut math for every branch row.
        var summary = await _ranks.GetSummaryAsync(memberId, ct);

        var allRanks = await _db.RankDefinitions.AsNoTracking()
            .Include(r => r.Requirements).OrderBy(r => r.SortOrder).ToListAsync(ct);

        var currentRankDef = allRanks.FirstOrDefault(r => r.SortOrder == summary.CurrentRankSortOrder);
        var nextRankDef    = allRanks.FirstOrDefault(r => r.SortOrder > summary.CurrentRankSortOrder);

        // Per-branch ET cap = MaxEnrollmentTeamPointsPerBranch × EnrollmentTeam.
        // The previous formula multiplied by TeamPoints (the DT threshold),
        // which yields 0 for Silver/Gold/Platinum (DT requirement = 0) and
        // therefore disables the cap entirely for the lower tiers.
        int CalcCap(Domain.Entities.Rank.RankDefinition? rankDef)
        {
            if (rankDef is null) return 0;
            var req = rankDef.Requirements.OrderBy(r => r.LevelNo).FirstOrDefault();
            if (req is null || req.EnrollmentTeam <= 0 || req.MaxEnrollmentTeamPointsPerBranch <= 0)
                return 0;
            return (int)Math.Round(req.MaxEnrollmentTeamPointsPerBranch * req.EnrollmentTeam);
        }

        var currentCap = CalcCap(currentRankDef);
        var nextCap    = nextRankDef is not null ? CalcCap(nextRankDef) : currentCap;

        var pageIds      = profiles.Select(p => p.MemberId).ToList();
        // pageStatsMap is derived from the already-fetched allStatsMap — no extra DB round-trip.
        var pageStatsMap = pageIds.ToDictionary(id => id, id => allStatsMap.GetValueOrDefault(id, 0));

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

        // The grand-total eligible count must never exceed the rank's own ET
        // threshold — otherwise the UI shows totals like "45 eligible toward
        // Silver" even though Silver only requires (and rewards) 18. Capping
        // here keeps the per-branch donuts (which still use the per-branch
        // cap) and the page-header total math consistent.
        var currentRankReq = currentRankDef?.Requirements.OrderBy(r => r.LevelNo).FirstOrDefault();
        var nextRankReq    = nextRankDef?.Requirements.OrderBy(r => r.LevelNo).FirstOrDefault();
        if (currentRankReq is { EnrollmentTeam: > 0 })
            totalEligibleCurrent = Math.Min(totalEligibleCurrent, currentRankReq.EnrollmentTeam);
        if (nextRankReq is { EnrollmentTeam: > 0 })
            totalEligibleNext    = Math.Min(totalEligibleNext,    nextRankReq.EnrollmentTeam);

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

    /// <summary>String DB columns the enrollment customers grid search box matches against.</summary>
    private static readonly string[] CustomerSearchableFields =
    {
        nameof(EnrollmentCustomerView.MemberId),
        nameof(EnrollmentCustomerView.FullName),
        nameof(EnrollmentCustomerView.Email),
        nameof(EnrollmentCustomerView.Phone),
        nameof(EnrollmentCustomerView.Country),
        nameof(EnrollmentCustomerView.AccountStatus),
    };

    // ─── Customers ─────────────────────────────────────────────────────────
    public async Task<PagedResult<EnrollmentCustomerView>> GetCustomersAsync(
        string memberId, int page, int pageSize, string? search,
        CancellationToken ct = default)
    {
        var myNode = await _db.GenealogyTree.AsNoTracking()
            .FirstOrDefaultAsync(g => g.MemberId == memberId, ct);
        if (myNode is null) return new PagedResult<EnrollmentCustomerView>();

        var q = BuildCustomerQueryable(myNode.HierarchyPath);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            q = q.Where(v => v.FullName.ToLower().Contains(s) || v.MemberId.ToLower().Contains(s));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(v => v.EnrollDate)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        await EnrichCustomerPageAsync(items, ct);

        return new PagedResult<EnrollmentCustomerView>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        };
    }

    /// <summary>
    /// Server-side grid read over the viewer's external-member customers — DB-side
    /// search/filter/sort/count/page on a light projection, page-only enrichment (was a
    /// whole-subtree Contains(allIds) load + in-memory page).
    /// </summary>
    public async Task<PagedResult<EnrollmentCustomerView>> GetCustomersGridAsync(
        string memberId, GridDataRequest request, CancellationToken ct = default)
    {
        var myNode = await _db.GenealogyTree.AsNoTracking()
            .FirstOrDefaultAsync(g => g.MemberId == memberId, ct);
        if (myNode is null) return new PagedResult<EnrollmentCustomerView>();

        if (!request.Sorts.Any(s => CustomerSearchableFields.Contains(s.Field, StringComparer.OrdinalIgnoreCase)
                                 || string.Equals(s.Field, nameof(EnrollmentCustomerView.EnrollDate), StringComparison.OrdinalIgnoreCase)))
            request.Sorts.Add(new GridSort { Field = nameof(EnrollmentCustomerView.EnrollDate), Direction = "desc" });

        var q      = BuildCustomerQueryable(myNode.HierarchyPath);
        var result = await q.ToGridResultAsync(request, CustomerSearchableFields, ct: ct);
        await EnrichCustomerPageAsync(result.Items, ct);
        return result;
    }

    /// <summary>Light DB projection of the viewer's external-member customers (profile columns
    /// only). Enriched columns (sponsor name, membership, points) filled per-page by
    /// <see cref="EnrichCustomerPageAsync"/>.</summary>
    private IQueryable<EnrollmentCustomerView> BuildCustomerQueryable(string pathPrefix)
    {
        return
            from g in _db.GenealogyTree.AsNoTracking()
            where g.HierarchyPath.StartsWith(pathPrefix)
            join m in _db.MemberProfiles.AsNoTracking() on g.MemberId equals m.MemberId
            where m.MemberType == MemberType.ExternalMember
            select new EnrollmentCustomerView
            {
                MemberId        = m.MemberId,
                FullName        = m.FirstName + " " + m.LastName,
                Email           = m.Email,
                Phone           = m.Phone,
                Country         = m.Country,
                EnrollDate      = m.EnrollDate,
                SponsorMemberId = m.SponsorMemberId,
                AccountStatus   = m.Status.ToString()
            };
    }

    private async Task EnrichCustomerPageAsync(
        IEnumerable<EnrollmentCustomerView> items, CancellationToken ct)
    {
        var rows = items as IList<EnrollmentCustomerView> ?? items.ToList();
        if (rows.Count == 0) return;
        var ids = rows.Select(r => r.MemberId).ToList();

        var subs = await _db.MembershipSubscriptions.AsNoTracking()
            .Include(s => s.MembershipLevel)
            .Where(s => ids.Contains(s.MemberId))
            .OrderByDescending(s => s.StartDate).ToListAsync(ct);
        var subMap = subs.GroupBy(s => s.MemberId).ToDictionary(g => g.Key, g => g.First());

        var statsMap = await _db.MemberStatistics.AsNoTracking()
            .Where(s => ids.Contains(s.MemberId))
            .ToDictionaryAsync(s => s.MemberId, ct);

        var sponsorIds = rows.Where(r => r.SponsorMemberId != null)
            .Select(r => r.SponsorMemberId!).Distinct().ToList();
        var nameMap = await _db.MemberProfiles.AsNoTracking()
            .Where(m => sponsorIds.Contains(m.MemberId))
            .Select(m => new { m.MemberId, FullName = m.FirstName + " " + m.LastName })
            .ToDictionaryAsync(m => m.MemberId, m => m.FullName, ct);

        foreach (var row in rows)
        {
            subMap.TryGetValue(row.MemberId, out var sub);
            statsMap.TryGetValue(row.MemberId, out var stat);
            nameMap.TryGetValue(row.SponsorMemberId ?? "", out var sponsorName);
            row.SponsorFullName  = sponsorName;
            row.MembershipStatus = sub?.SubscriptionStatus.ToString() ?? "None";
            row.MembershipLevel  = sub?.MembershipLevel?.Name;
            row.PersonalPoints   = stat?.PersonalPoints ?? 0;
        }
    }

    // ─── Visualizer Stats ──────────────────────────────────────────────────
    public async Task<EnrollmentVisualizerStatsView> GetVisualizerStatsAsync(
        string memberId, CancellationToken ct = default)
    {
        var pattern = "/" + memberId + "/";

        // Single grouped JOIN — count by status in SQL instead of pulling all 120k+ downline
        // ids into memory and re-querying with Contains(allIds).
        var statusCounts = await (
            from g in _db.GenealogyTree.AsNoTracking()
            where g.HierarchyPath.Contains(pattern)
            join m in _db.MemberProfiles.AsNoTracking() on g.MemberId equals m.MemberId
            group m by m.Status into grp
            select new { Status = grp.Key, Count = grp.Count() }
        ).ToListAsync(ct);

        if (statusCounts.Count == 0) return new EnrollmentVisualizerStatsView();

        return new EnrollmentVisualizerStatsView
        {
            TotalMembers     = statusCounts.Sum(x => x.Count),
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
