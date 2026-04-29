using Microsoft.EntityFrameworkCore;
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

        // Lifetime rank — highest SortOrder ever achieved (preserved from history).
        var lifetimeRank = await _db.MemberRankHistories.AsNoTracking()
            .Include(r => r.RankDefinition)
            .Where(r => r.MemberId == memberId)
            .OrderByDescending(r => r.RankDefinition!.SortOrder)
            .Select(r => r.RankDefinition!.Name)
            .FirstOrDefaultAsync(ct);

        // All ranks + their headline requirement (lowest LevelNo per rank — works
        // for both LevelNo=0 and LevelNo=SortOrder data conventions).
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

        var dt = stats?.DualTeamPoints   ?? 0;
        var ep = stats?.EnrollmentPoints ?? 0;

        // Live qualification: highest rank where the member meets BOTH thresholds.
        // A threshold of 0 means that requirement does not apply for that rank
        // (e.g. Silver/Gold/Platinum have no DT requirement).
        var currentRankRow = ranksWithReq
            .Where(r => r.Req != null
                     && (r.Req.TeamPoints     == 0 || dt >= r.Req.TeamPoints)
                     && (r.Req.EnrollmentTeam == 0 || ep >= r.Req.EnrollmentTeam))
            .OrderByDescending(r => r.SortOrder)
            .FirstOrDefault();

        var currentSortOrder = currentRankRow?.SortOrder ?? 0;
        var nextRankRow      = ranksWithReq.FirstOrDefault(r => r.SortOrder > currentSortOrder);

        return new RankSummaryDto
        {
            MemberId                    = memberId,

            CurrentRankName             = currentRankRow?.Name,
            CurrentRankSortOrder        = currentSortOrder,
            CurrentRankDualTeamPoints   = currentRankRow?.Req?.TeamPoints     ?? 0,
            CurrentRankEnrollmentPoints = currentRankRow?.Req?.EnrollmentTeam ?? 0,

            NextRankName                = nextRankRow?.Name,
            NextRankSortOrder           = nextRankRow?.SortOrder ?? 0,
            NextRankDualTeamPoints      = nextRankRow?.Req?.TeamPoints     ?? 0,
            NextRankEnrollmentPoints    = nextRankRow?.Req?.EnrollmentTeam ?? 0,

            LifetimeRankName            = lifetimeRank,

            DualTeamPoints              = stats?.DualTeamPoints            ?? 0,
            EnrollmentPoints            = stats?.EnrollmentPoints          ?? 0,
            QualifiedSponsoredMembers   = stats?.QualifiedSponsoredMembers ?? 0,
            EnrollmentTeamSize          = stats?.EnrollmentTeamSize        ?? 0
        };
    }
}
