using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.Ranks;

/// <inheritdoc />
public class RankComputationService : IRankComputationService
{
    private readonly AppDbContext _db;

    public RankComputationService(AppDbContext db) => _db = db;

    public async Task<RankSummaryDto> GetSummaryAsync(string memberId, CancellationToken ct = default)
    {
        var stats = await _db.MemberStatistics.AsNoTracking()
            .FirstOrDefaultAsync(s => s.MemberId == memberId, ct);

        // Per-direct-branch enrollment-team points. The rank rule caps each
        // branch's contribution at MaxEnrollmentTeamPointsPerBranch * threshold,
        // so we MUST evaluate qualification on the capped sum, not the raw
        // EnrollmentPoints roll-up. Using the raw total would let a single
        // dominant branch float the member into a rank they cannot actually
        // hold under the per-leg rule.
        var directChildIds = await _db.GenealogyTree.AsNoTracking()
            .Where(g => g.ParentMemberId == memberId)
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        var branchPoints = directChildIds.Count > 0
            ? await _db.MemberStatistics.AsNoTracking()
                  .Where(s => directChildIds.Contains(s.MemberId))
                  .Select(s => s.EnrollmentPoints)
                  .ToListAsync(ct)
            : new List<int>();

        // Dual-team L/R leg points for the binary cap. Same rationale: the
        // rule requires each leg's contribution to be capped at
        // MaxTeamPointsPerBranch * threshold before summing.
        var dualNode = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == memberId, ct);
        var leftLeg  = (int)(dualNode?.LeftLegPoints  ?? 0);
        var rightLeg = (int)(dualNode?.RightLegPoints ?? 0);

        // Lifetime rank — highest SortOrder ever achieved (preserved from history).
        var lifetimeHistory = await _db.MemberRankHistories.AsNoTracking()
            .Include(r => r.RankDefinition)
            .Where(r => r.MemberId == memberId)
            .OrderByDescending(r => r.RankDefinition!.SortOrder)
            .Select(r => new { r.RankDefinitionId, r.RankDefinition!.Name })
            .FirstOrDefaultAsync(ct);

        var ranksWithReq = await _db.RankDefinitions.AsNoTracking()
            .OrderBy(r => r.SortOrder)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.SortOrder,
                Req = r.Requirements.OrderBy(rq => rq.LevelNo).FirstOrDefault()
            })
            .ToListAsync(ct);

        // Live qualification: highest rank where the capped totals meet BOTH
        // thresholds. Threshold = 0 means "this dimension does not apply at
        // this rank" (Silver/Gold/Platinum have no DT requirement; some legacy
        // ranks may have no ET requirement).
        var currentRankRow = ranksWithReq
            .Where(r => r.Req != null && QualifiesFor(r.Req, branchPoints, leftLeg, rightLeg))
            .OrderByDescending(r => r.SortOrder)
            .FirstOrDefault();

        var currentSortOrder = currentRankRow?.SortOrder ?? 0;
        var nextRankRow      = ranksWithReq.FirstOrDefault(r => r.SortOrder > currentSortOrder);

        return new RankSummaryDto
        {
            MemberId                            = memberId,

            CurrentRankName                     = currentRankRow?.Name,
            CurrentRankId                       = currentRankRow?.Id,
            CurrentRankSortOrder                = currentSortOrder,
            CurrentRankDualTeamPoints           = currentRankRow?.Req?.TeamPoints     ?? 0,
            CurrentRankEnrollmentPoints         = currentRankRow?.Req?.EnrollmentTeam ?? 0,
            CurrentRankEligibleDualTeamPoints   = CappedDualTeamTotal(currentRankRow?.Req, leftLeg, rightLeg),
            CurrentRankEligibleEnrollmentPoints = CappedEnrollmentTotal(currentRankRow?.Req, branchPoints),

            NextRankName                        = nextRankRow?.Name,
            NextRankId                          = nextRankRow?.Id,
            NextRankSortOrder                   = nextRankRow?.SortOrder ?? 0,
            NextRankDualTeamPoints              = nextRankRow?.Req?.TeamPoints     ?? 0,
            NextRankEnrollmentPoints            = nextRankRow?.Req?.EnrollmentTeam ?? 0,
            NextRankEligibleDualTeamPoints      = CappedDualTeamTotal(nextRankRow?.Req, leftLeg, rightLeg),
            NextRankEligibleEnrollmentPoints    = CappedEnrollmentTotal(nextRankRow?.Req, branchPoints),

            LifetimeRankName                    = lifetimeHistory?.Name,
            LifetimeRankId                      = lifetimeHistory?.RankDefinitionId,

            DualTeamPoints                      = stats?.DualTeamPoints            ?? 0,
            EnrollmentPoints                    = stats?.EnrollmentPoints          ?? 0,
            QualifiedSponsoredMembers           = stats?.QualifiedSponsoredMembers ?? 0,
            EnrollmentTeamSize                  = stats?.EnrollmentTeamSize        ?? 0
        };
    }

    /// <summary>
    /// True when the member meets BOTH dimensions of the requirement after
    /// applying the per-leg / per-branch caps prescribed by the rank table.
    /// A zero threshold opts that dimension out of the test for this rank.
    /// </summary>
    private static bool QualifiesFor(
        RankRequirement req,
        List<int> branchPoints,
        int leftLeg,
        int rightLeg)
    {
        if (req.EnrollmentTeam > 0
            && CappedEnrollmentTotal(req, branchPoints) < req.EnrollmentTeam) return false;

        if (req.TeamPoints > 0
            && CappedDualTeamTotal(req, leftLeg, rightLeg) < req.TeamPoints) return false;

        return true;
    }

    /// <summary>
    /// Sum of branch ET points after applying the per-branch cap, then capped
    /// at the rank's EnrollmentTeam threshold. Returns 0 when the requirement
    /// has no ET dimension (callers should pair this with the raw threshold to
    /// decide whether to surface the value).
    /// </summary>
    private static int CappedEnrollmentTotal(RankRequirement? req, List<int> branchPoints)
    {
        if (req is null || req.EnrollmentTeam <= 0) return 0;

        var perBranchCap = req.MaxEnrollmentTeamPointsPerBranch > 0
            ? (int)Math.Round(req.MaxEnrollmentTeamPointsPerBranch * req.EnrollmentTeam)
            : 0;

        var summed = perBranchCap > 0
            ? branchPoints.Sum(p => Math.Min(p, perBranchCap))
            : branchPoints.Sum();

        return Math.Min(summed, req.EnrollmentTeam);
    }

    /// <summary>
    /// Sum of L/R leg DT points after applying the per-leg cap, then capped at
    /// the rank's TeamPoints threshold. Returns 0 when the requirement has no
    /// DT dimension.
    /// </summary>
    private static int CappedDualTeamTotal(RankRequirement? req, int leftLeg, int rightLeg)
    {
        if (req is null || req.TeamPoints <= 0) return 0;

        var perLegCap = req.MaxTeamPointsPerBranch > 0
            ? (int)Math.Round(req.MaxTeamPointsPerBranch * req.TeamPoints)
            : 0;

        var summed = perLegCap > 0
            ? Math.Min(leftLeg, perLegCap) + Math.Min(rightLeg, perLegCap)
            : leftLeg + rightLeg;

        return Math.Min(summed, req.TeamPoints);
    }
}
