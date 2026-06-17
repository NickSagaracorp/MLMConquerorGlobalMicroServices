using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.Ranks;

/// <inheritdoc />
public class RankComputationService : IRankComputationService
{
    private readonly AppDbContext _db;
    private readonly IRankQualificationService _qualification;

    public RankComputationService(AppDbContext db, IRankQualificationService qualification)
    {
        _db = db;
        _qualification = qualification;
    }

    public async Task<RankSummaryDto> GetSummaryAsync(string memberId, CancellationToken ct = default)
    {
        var stats = await _db.MemberStatistics.AsNoTracking()
            .FirstOrDefaultAsync(s => s.MemberId == memberId, ct);

        var lifetimeHistory = await _db.MemberRankHistories.AsNoTracking()
            .Include(r => r.RankDefinition)
            .Where(r => r.MemberId == memberId)
            .OrderByDescending(r => r.RankDefinition!.SortOrder)
            .Select(r => new { r.RankDefinitionId, r.RankDefinition!.Name })
            .FirstOrDefaultAsync(ct);

        var ranks = await _db.RankDefinitions.AsNoTracking()
            .Where(r => r.Status == RankDefinitionStatus.Active)
            .OrderBy(r => r.SortOrder)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.SortOrder,
                Req = r.Requirements.OrderBy(rq => rq.LevelNo).FirstOrDefault()
            })
            .ToListAsync(ct);

        // Evaluate ALL ranks against a SINGLE member snapshot. The previous code looped
        // QualifiesForRankAsync per rank (~20 calls), and each call re-loaded the full
        // qualification snapshot (gate + dual leg + enrollment branches + stats + external +
        // orders) — so GetSummaryAsync did ~20× the DB work (≈60s for a 120k-downline member).
        // QualifiesForAllRanksAsync loads the snapshot once and reuses it across all ranks.
        var reqs = ranks.Where(r => r.Req is not null).Select(r => r.Req!).ToList();
        var evaluations = await _qualification.QualifiesForAllRanksAsync(memberId, reqs, ct);
        var resultByReqId = evaluations.ToDictionary(e => e.Requirement.Id, e => e.Result);

        // Highest rank the member qualifies for, evaluated through the single authority.
        (int SortOrder, int Id, string Name, int DtThreshold, int EtThreshold,
         int EligibleDt, int EligibleEt)? current = null;

        foreach (var rank in ranks)
        {
            if (rank.Req is null) continue;
            if (!resultByReqId.TryGetValue(rank.Req.Id, out var result) || !result.Qualifies)
                continue;
            current = (rank.SortOrder, rank.Id, rank.Name,
                rank.Req.TeamPoints, rank.Req.EnrollmentTeam,
                result.EligibleDualTeamPoints, result.EligibleEnrollmentTeamPoints);
        }

        var currentSortOrder = current?.SortOrder ?? 0;
        var nextRank = ranks.FirstOrDefault(r => r.SortOrder > currentSortOrder);

        var nextEligibleDt = 0;
        var nextEligibleEt = 0;
        if (nextRank?.Req is not null && resultByReqId.TryGetValue(nextRank.Req.Id, out var nextResult))
        {
            nextEligibleDt = nextResult.EligibleDualTeamPoints;
            nextEligibleEt = nextResult.EligibleEnrollmentTeamPoints;
        }

        return new RankSummaryDto
        {
            MemberId = memberId,

            CurrentRankName                     = current?.Name,
            CurrentRankId                       = current?.Id,
            CurrentRankSortOrder                = currentSortOrder,
            CurrentRankDualTeamPoints           = current?.DtThreshold ?? 0,
            CurrentRankEnrollmentPoints         = current?.EtThreshold ?? 0,
            CurrentRankEligibleDualTeamPoints   = current?.EligibleDt ?? 0,
            CurrentRankEligibleEnrollmentPoints = current?.EligibleEt ?? 0,

            NextRankName                        = nextRank?.Name,
            NextRankId                          = nextRank?.Id,
            NextRankSortOrder                   = nextRank?.SortOrder ?? 0,
            NextRankDualTeamPoints              = nextRank?.Req?.TeamPoints     ?? 0,
            NextRankEnrollmentPoints            = nextRank?.Req?.EnrollmentTeam ?? 0,
            NextRankEligibleDualTeamPoints      = nextEligibleDt,
            NextRankEligibleEnrollmentPoints    = nextEligibleEt,

            LifetimeRankName                    = lifetimeHistory?.Name,
            LifetimeRankId                      = lifetimeHistory?.RankDefinitionId,

            DualTeamPoints                      = stats?.DualTeamPoints            ?? 0,
            EnrollmentPoints                    = stats?.EnrollmentPoints          ?? 0,
            QualifiedSponsoredMembers           = stats?.QualifiedSponsoredMembers ?? 0,
            EnrollmentTeamSize                  = stats?.EnrollmentTeamSize        ?? 0
        };
    }
}
