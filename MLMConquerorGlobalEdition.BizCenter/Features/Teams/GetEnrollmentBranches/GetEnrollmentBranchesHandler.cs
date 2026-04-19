using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetEnrollmentBranches;

public class GetEnrollmentBranchesHandler
    : IRequestHandler<GetEnrollmentBranchesQuery, Result<EnrollmentBranchesResultDto>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;

    public GetEnrollmentBranchesHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<Result<EnrollmentBranchesResultDto>> Handle(
        GetEnrollmentBranchesQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        // ── 1. Resolve the current member's genealogy node ───────────────────────
        var myNode = await _db.GenealogyTree
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.MemberId == memberId, ct);

        if (myNode is null)
            return Result<EnrollmentBranchesResultDto>.Success(new EnrollmentBranchesResultDto());

        // ── 2. Get all direct children (Level == myNode.Level + 1 AND parent == me) ──
        var directChildIds = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => g.ParentMemberId == memberId)
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        if (!directChildIds.Any())
            return Result<EnrollmentBranchesResultDto>.Success(new EnrollmentBranchesResultDto());

        // ── 3. Member profiles for direct children, optional search filter ────────
        var profileQuery = _db.MemberProfiles
            .AsNoTracking()
            .Where(m => directChildIds.Contains(m.MemberId));

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.ToLower();
            profileQuery = profileQuery.Where(m =>
                m.FirstName.ToLower().Contains(s) ||
                m.LastName.ToLower().Contains(s)  ||
                m.MemberId.ToLower().Contains(s));
        }

        var totalCount = await profileQuery.CountAsync(ct);

        var profiles = await profileQuery
            .OrderBy(m => m.FirstName)
            .ThenBy(m => m.LastName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new { m.MemberId, m.FirstName, m.LastName })
            .ToListAsync(ct);

        var allFilteredIds = await profileQuery
            .Select(m => m.MemberId)
            .ToListAsync(ct);

        // ── 4. Member statistics for ALL filtered children (for summary totals) ───
        var allStats = await _db.MemberStatistics
            .AsNoTracking()
            .Where(s => allFilteredIds.Contains(s.MemberId))
            .Select(s => new { s.MemberId, s.EnrollmentPoints })
            .ToListAsync(ct);

        var allStatsMap = allStats.ToDictionary(s => s.MemberId, s => s.EnrollmentPoints);

        // ── 5. Resolve current member's rank ─────────────────────────────────────
        var currentMemberRank = await _db.MemberRankHistories
            .AsNoTracking()
            .Include(r => r.RankDefinition)
            .Where(r => r.MemberId == memberId)
            .OrderByDescending(r => r.AchievedAt)
            .FirstOrDefaultAsync(ct);

        var currentRankSortOrder = currentMemberRank?.RankDefinition?.SortOrder ?? 0;

        // ── 6. Load all rank definitions with requirements (ordered by SortOrder) ─
        var allRanks = await _db.RankDefinitions
            .AsNoTracking()
            .Include(r => r.Requirements)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        // Current rank definition (exact match by SortOrder)
        var currentRankDef = allRanks.FirstOrDefault(r => r.SortOrder == currentRankSortOrder);
        // Next rank definition
        var nextRankDef = allRanks.FirstOrDefault(r => r.SortOrder > currentRankSortOrder);

        // ── 7. Derive per-branch leg caps from rank requirements ──────────────────
        // MaxEnrollmentTeamPointsPerBranch is the fractional cap (e.g. 0.5 = 50% of TeamPoints).
        // The actual leg cap = MaxEnrollmentTeamPointsPerBranch * TeamPoints from LevelNo == 0 requirement.
        // LevelNo == 0 is the top-level (global) requirement row for a rank.

        int CurrentLegCap()
        {
            if (currentRankDef is null) return 0;
            var req = currentRankDef.Requirements.FirstOrDefault(r => r.LevelNo == 0);
            if (req is null || req.TeamPoints <= 0) return 0;
            return (int)Math.Round(req.MaxEnrollmentTeamPointsPerBranch * req.TeamPoints);
        }

        int NextLegCap()
        {
            if (nextRankDef is null)
            {
                // Already at highest rank — reuse current cap
                return CurrentLegCap();
            }
            var req = nextRankDef.Requirements.FirstOrDefault(r => r.LevelNo == 0);
            if (req is null || req.TeamPoints <= 0) return 0;
            return (int)Math.Round(req.MaxEnrollmentTeamPointsPerBranch * req.TeamPoints);
        }

        var currentCap = CurrentLegCap();
        var nextCap    = NextLegCap();

        // ── 8. Statistics for paged profiles only (for the table rows) ────────────
        var pageIds   = profiles.Select(p => p.MemberId).ToList();
        var pageStats = await _db.MemberStatistics
            .AsNoTracking()
            .Where(s => pageIds.Contains(s.MemberId))
            .Select(s => new { s.MemberId, s.EnrollmentPoints })
            .ToListAsync(ct);

        var pageStatsMap = pageStats.ToDictionary(s => s.MemberId, s => s.EnrollmentPoints);

        // ── 9. Compute summary totals across ALL filtered children ────────────────
        var totalPoints = allStatsMap.Values.Sum();

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

        // ── 10. Assemble per-row DTOs ─────────────────────────────────────────────
        var branchItems = profiles.Select(p =>
        {
            var rawPts = pageStatsMap.TryGetValue(p.MemberId, out var sp) ? sp : 0;

            var eligCurrent = currentCap > 0 ? Math.Min(rawPts, currentCap) : rawPts;
            var eligNext    = nextCap    > 0 ? Math.Min(rawPts, nextCap)    : rawPts;

            var pctCurrent = currentCap > 0
                ? Math.Min(100, eligCurrent * 100 / currentCap)
                : (rawPts > 0 ? 100 : 0);

            var pctNext = nextCap > 0
                ? Math.Min(100, eligNext * 100 / nextCap)
                : (rawPts > 0 ? 100 : 0);

            return new BranchItemDto
            {
                MemberId            = p.MemberId,
                FullName            = $"{p.FirstName} {p.LastName}",
                TotalPoints         = rawPts,
                EligibleCurrentRank = eligCurrent,
                EligibleNextRank    = eligNext,
                EligibleCurrentPct  = pctCurrent,
                EligibleNextPct     = pctNext
            };
        }).ToList();

        // ── 11. Build result ──────────────────────────────────────────────────────
        var result = new EnrollmentBranchesResultDto
        {
            TotalPoints              = totalPoints,
            TotalEligibleCurrentRank = totalEligibleCurrent,
            TotalEligibleNextRank    = totalEligibleNext,
            Branches = new BranchPagedData
            {
                Items      = branchItems,
                TotalCount = totalCount,
                Page       = request.Page,
                PageSize   = request.PageSize
            }
        };

        return Result<EnrollmentBranchesResultDto>.Success(result);
    }
}
