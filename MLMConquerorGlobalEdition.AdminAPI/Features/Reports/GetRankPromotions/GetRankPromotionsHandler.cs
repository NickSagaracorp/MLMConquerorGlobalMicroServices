using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Reports;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Reports.GetRankPromotions;

public class GetRankPromotionsHandler : IRequestHandler<GetRankPromotionsQuery, Result<IEnumerable<RankPromotionDto>>>
{
    private readonly AppDbContext _db;

    public GetRankPromotionsHandler(AppDbContext db) => _db = db;

    public async Task<Result<IEnumerable<RankPromotionDto>>> Handle(
        GetRankPromotionsQuery query, CancellationToken ct)
    {
        // Normalize range to full UTC days
        var from = query.From.Date;
        var to   = query.To.Date.AddDays(1).AddTicks(-1);

        // Load rank history records that fall inside the requested range.
        // Keep only the absolute FIRST time each (MemberId, RankDefinitionId) pair was ever achieved —
        // i.e., no earlier record exists for the same member + rank combination.
        var promotions = await _db.MemberRankHistories
            .AsNoTracking()
            .Include(h => h.RankDefinition)
            .Where(h =>
                !h.IsDeleted &&
                h.AchievedAt >= from &&
                h.AchievedAt <= to &&
                (query.RankDefinitionId == null || h.RankDefinitionId == query.RankDefinitionId) &&
                // Exclude re-promotions: no older record with the same member + rank must exist
                !_db.MemberRankHistories.Any(h2 =>
                    h2.MemberId          == h.MemberId &&
                    h2.RankDefinitionId  == h.RankDefinitionId &&
                    h2.AchievedAt        <  h.AchievedAt &&
                    !h2.IsDeleted))
            .OrderBy(h => h.RankDefinition!.SortOrder)
            .ThenBy(h => h.AchievedAt)
            .ToListAsync(ct);

        // Collect distinct MemberIds so we can join member data in a single query
        var memberIds = promotions.Select(h => h.MemberId).Distinct().ToList();
        var members = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => memberIds.Contains(m.MemberId))
            .ToDictionaryAsync(m => m.MemberId, ct);

        // Collect PreviousRankId values for a single rank definition lookup
        var previousRankIds = promotions
            .Where(h => h.PreviousRankId.HasValue)
            .Select(h => h.PreviousRankId!.Value)
            .Distinct()
            .ToList();

        var previousRanks = previousRankIds.Count > 0
            ? await _db.RankDefinitions
                .AsNoTracking()
                .Where(r => previousRankIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, ct)
            : new Dictionary<int, Domain.Entities.Rank.RankDefinition>();

        var result = promotions
            .Where(h => members.ContainsKey(h.MemberId) && h.RankDefinition is not null)
            .Select(h =>
            {
                var member       = members[h.MemberId];
                var previousRank = h.PreviousRankId.HasValue && previousRanks.TryGetValue(h.PreviousRankId.Value, out var pr)
                    ? pr.Name
                    : null;

                return new RankPromotionDto(
                    MemberId:        h.MemberId,
                    FullName:        $"{member.FirstName} {member.LastName}".Trim(),
                    Email:           member.Email,
                    RankDefinitionId: h.RankDefinitionId,
                    RankName:        h.RankDefinition!.Name,
                    RankSortOrder:   h.RankDefinition.SortOrder,
                    AchievedAt:      h.AchievedAt,
                    PreviousRankName: previousRank);
            })
            .ToList();

        return Result<IEnumerable<RankPromotionDto>>.Success(result);
    }
}
