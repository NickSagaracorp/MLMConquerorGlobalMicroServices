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

        // Highest rank the member qualifies for, evaluated through the single authority.
        (int SortOrder, int Id, string Name, int DtThreshold, int EtThreshold,
         int EligibleDt, int EligibleEt)? current = null;

        foreach (var rank in ranks)
        {
            if (rank.Req is null)
                continue;
            var result = await _qualification.QualifiesForRankAsync(memberId, rank.Req, ct);
            if (!result.Qualifies)
                continue;
            current = (rank.SortOrder, rank.Id, rank.Name,
                rank.Req.TeamPoints, rank.Req.EnrollmentTeam,
                result.EligibleDualTeamPoints, result.EligibleEnrollmentTeamPoints);
        }

        var currentSortOrder = current?.SortOrder ?? 0;
        var nextRank = ranks.FirstOrDefault(r => r.SortOrder > currentSortOrder);

        var nextEligibleDt = 0;
        var nextEligibleEt = 0;
        if (nextRank?.Req is not null)
        {
            var nextResult = await _qualification.QualifiesForRankAsync(memberId, nextRank.Req, ct);
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
