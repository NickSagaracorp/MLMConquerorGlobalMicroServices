using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.RankRequirements;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.RankRequirements.GetRankDashboard;

public class GetRankDashboardHandler : IRequestHandler<GetRankDashboardQuery, Result<RankDashboardDto>>
{
    private readonly AppDbContext _db;

    public GetRankDashboardHandler(AppDbContext db) => _db = db;

    public async Task<Result<RankDashboardDto>> Handle(
        GetRankDashboardQuery request, CancellationToken cancellationToken)
    {
        // Get all active rank definitions ordered by sort order
        var rankDefinitions = await _db.RankDefinitions
            .AsNoTracking()
            .Where(r => r.Status == RankDefinitionStatus.Active)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(cancellationToken);

        // For each member, find their current (latest) rank
        var memberCurrentRanks = await _db.MemberRankHistories
            .AsNoTracking()
            .GroupBy(h => h.MemberId)
            .Select(g => g.OrderByDescending(h => h.AchievedAt).First())
            .GroupBy(h => h.RankDefinitionId)
            .Select(g => new { RankId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var totalRankedMembers = memberCurrentRanks.Sum(x => x.Count);
        var countLookup = memberCurrentRanks.ToDictionary(x => x.RankId, x => x.Count);

        var rankStats = rankDefinitions.Select(r =>
        {
            var count = countLookup.TryGetValue(r.Id, out var c) ? c : 0;
            var percentage = totalRankedMembers > 0
                ? Math.Round((decimal)count / totalRankedMembers * 100, 2)
                : 0m;

            return new RankMemberCountDto
            {
                RankId = r.Id,
                RankName = r.Name,
                MemberCount = count,
                Percentage = percentage
            };
        }).ToList();

        return Result<RankDashboardDto>.Success(new RankDashboardDto
        {
            Ranks = rankStats,
            TotalRankedMembers = totalRankedMembers
        });
    }
}
