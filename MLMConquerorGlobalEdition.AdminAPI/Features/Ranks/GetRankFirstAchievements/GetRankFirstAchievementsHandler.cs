using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Ranks;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Ranks.GetRankFirstAchievements;

public class GetRankFirstAchievementsHandler
    : IRequestHandler<GetRankFirstAchievementsQuery, Result<PagedResult<RankFirstAchievementRowDto>>>
{
    private readonly AppDbContext _db;
    public GetRankFirstAchievementsHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<RankFirstAchievementRowDto>>> Handle(
        GetRankFirstAchievementsQuery r, CancellationToken ct)
    {
        var page = r.Page < 1 ? 1 : r.Page;
        var pageSize = r.PageSize < 1 ? 20 : r.PageSize;

        // First-ever achievement per (member, rank): MIN(AchievedAt).
        var firstPerMemberRank = _db.MemberRankHistories
            .Where(h => !h.IsDeleted && (r.RankDefinitionId == null || h.RankDefinitionId == r.RankDefinitionId))
            .GroupBy(h => new { h.MemberId, h.RankDefinitionId })
            .Select(g => new { g.Key.MemberId, g.Key.RankDefinitionId, FirstAchievedAt = g.Min(x => x.AchievedAt) });

        // Keep only those whose first-ever achievement falls in the selected month+year.
        var inMonth = firstPerMemberRank
            .Where(x => x.FirstAchievedAt.Year == r.Year && x.FirstAchievedAt.Month == r.Month);

        var total = await inMonth.CountAsync(ct);

        var pageRows = await inMonth
            .OrderBy(x => x.FirstAchievedAt).ThenBy(x => x.MemberId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var memberIds = pageRows.Select(x => x.MemberId).Distinct().ToList();
        var rankIds = pageRows.Select(x => x.RankDefinitionId).Distinct().ToList();

        var names = await _db.MemberProfiles.AsNoTracking()
            .Where(m => memberIds.Contains(m.MemberId))
            .Select(m => new { m.MemberId, FullName = (m.FirstName + " " + m.LastName).Trim() })
            .ToDictionaryAsync(m => m.MemberId, m => m.FullName, ct);

        // Fetch the first-achievement MemberRankHistory rows for the page so we can read PreviousRankId.
        // Each page row is identified by (MemberId, RankDefinitionId, FirstAchievedAt).
        var firstHistoryRows = new Dictionary<(string MemberId, int RankId), int?>();
        foreach (var row in pageRows)
        {
            var histRow = await _db.MemberRankHistories.AsNoTracking()
                .Where(h => !h.IsDeleted
                         && h.MemberId == row.MemberId
                         && h.RankDefinitionId == row.RankDefinitionId
                         && h.AchievedAt == row.FirstAchievedAt)
                .Select(h => new { h.PreviousRankId })
                .FirstOrDefaultAsync(ct);
            firstHistoryRows[(row.MemberId, row.RankDefinitionId)] = histRow?.PreviousRankId;
        }

        // Collect all rank ids needed for display, including previous-rank ids.
        var allRankIds = rankIds
            .Concat(firstHistoryRows.Values.Where(v => v.HasValue).Select(v => v!.Value))
            .Distinct()
            .ToList();

        var ranks = await _db.RankDefinitions.AsNoTracking()
            .Where(d => allRankIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Name, d.SortOrder })
            .ToDictionaryAsync(d => d.Id, ct);

        var items = pageRows.Select(x =>
        {
            firstHistoryRows.TryGetValue((x.MemberId, x.RankDefinitionId), out var prevRankId);
            string? previousRankName = prevRankId.HasValue && ranks.TryGetValue(prevRankId.Value, out var pr)
                ? pr.Name : null;

            return new RankFirstAchievementRowDto
            {
                MemberId = x.MemberId,
                FullName = names.TryGetValue(x.MemberId, out var n) ? n : string.Empty,
                RankDefinitionId = x.RankDefinitionId,
                RankName = ranks.TryGetValue(x.RankDefinitionId, out var d) ? d.Name : string.Empty,
                RankSortOrder = ranks.TryGetValue(x.RankDefinitionId, out var d2) ? d2.SortOrder : 0,
                AchievedAt = x.FirstAchievedAt,
                PreviousRankName = previousRankName
            };
        }).ToList();

        return Result<PagedResult<RankFirstAchievementRowDto>>.Success(new PagedResult<RankFirstAchievementRowDto>
        { Items = items, TotalCount = total, Page = page, PageSize = pageSize });
    }
}
