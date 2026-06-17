using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Ranks;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Ranks.GetRankSeniorityCandidates;

public class GetRankSeniorityCandidatesHandler
    : IRequestHandler<GetRankSeniorityCandidatesQuery, Result<PagedResult<RankSeniorityRowDto>>>
{
    private const int WindowDays = 40;
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    public GetRankSeniorityCandidatesHandler(AppDbContext db, IDateTimeProvider dateTime)
    { _db = db; _dateTime = dateTime; }

    public async Task<Result<PagedResult<RankSeniorityRowDto>>> Handle(
        GetRankSeniorityCandidatesQuery r, CancellationToken ct)
    {
        var page = r.Page < 1 ? 1 : r.Page;
        var pageSize = r.PageSize < 1 ? 20 : r.PageSize;
        var today = _dateTime.Now.Date;
        var windowStart = today.AddDays(-WindowDays);
        // A current streak's most recent day must be "fresh" (yesterday or today — tolerate the nightly cadence).
        var freshFrom = today.AddDays(-1);

        // Lifetime rank per member (highest SortOrder).
        var lifetime = await _db.MemberRankHistories.AsNoTracking()
            .Where(h => !h.IsDeleted)
            .Select(h => new { h.MemberId, h.RankDefinitionId, h.RankDefinition!.SortOrder })
            .ToListAsync(ct);
        var lifetimeByMember = lifetime
            .GroupBy(h => h.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.SortOrder).First().RankDefinitionId);

        // Daily residual rows in the window, thin projection.
        // DailyResidualEarning inherits AuditChangesLongKey which has no IsDeleted.
        var daily = await _db.DailyResidualEarnings.AsNoTracking()
            .Where(e => e.CurrentRankId != null && e.EarnedDate >= windowStart)
            .Select(e => new { e.BeneficiaryMemberId, e.CurrentRankId, e.EarnedDate })
            .ToListAsync(ct);

        // Per-rank seniority CommissionType (id + amount).
        var seniorityTypes = await _db.CommissionTypes.AsNoTracking()
            .Where(t => t.CommissionCategoryId == RankSeniorityBonus.CategoryId)
            .Select(t => new { t.Id, t.LifeTimeRank, t.Amount })
            .ToListAsync(ct);
        var typeByRank = seniorityTypes.ToDictionary(t => t.LifeTimeRank, t => new { t.Id, Amount = t.Amount ?? 0m });
        var seniorityTypeIds = seniorityTypes.Select(t => t.Id).ToHashSet();

        // Members already granted ANY seniority bonus, keyed by (member, rank) via the type's LifeTimeRank.
        var grantedTypeIdByRank = seniorityTypes.ToDictionary(t => t.Id, t => t.LifeTimeRank);
        var grantedRows = await _db.CommissionEarnings.AsNoTracking()
            .Where(e => seniorityTypeIds.Contains(e.CommissionTypeId))
            .Select(e => new { e.BeneficiaryMemberId, e.CommissionTypeId })
            .ToListAsync(ct);
        var grantedMemberRank = grantedRows
            .Select(g => (g.BeneficiaryMemberId, Rank: grantedTypeIdByRank[g.CommissionTypeId]))
            .ToHashSet();

        var rankNames = await _db.RankDefinitions.AsNoTracking()
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        var dailyByMember = daily.GroupBy(d => d.BeneficiaryMemberId);
        var rows = new List<RankSeniorityRowDto>();

        foreach (var grp in dailyByMember)
        {
            var memberId = grp.Key;
            if (!lifetimeByMember.TryGetValue(memberId, out var lifeRank)) continue;
            if (r.RankDefinitionId is int rf && lifeRank != rf) continue;
            if (grantedMemberRank.Contains((memberId, lifeRank))) continue; // already granted that rank

            // Order this member's window rows newest-first, by calendar day.
            var byDayDesc = grp
                .GroupBy(x => x.EarnedDate.Date)
                .Select(g => new { Day = g.Key, RankId = g.OrderByDescending(z => z.EarnedDate).First().CurrentRankId!.Value })
                .OrderByDescending(x => x.Day)
                .ToList();

            var mostRecent = byDayDesc[0];
            // Must be currently settled at the lifetime rank, and the streak must be fresh.
            if (mostRecent.RankId != lifeRank) continue;
            if (mostRecent.Day < freshFrom) continue;

            // Walk the unbroken consecutive-day run at lifeRank ending at the most recent day.
            var streak = 1;
            var prevDay = mostRecent.Day;
            for (var i = 1; i < byDayDesc.Count; i++)
            {
                var cur = byDayDesc[i];
                if (cur.RankId == lifeRank && cur.Day == prevDay.AddDays(-1)) { streak++; prevDay = cur.Day; }
                else break;
            }

            if (streak < r.MinDays) continue;

            rows.Add(new RankSeniorityRowDto
            {
                MemberId = memberId,
                RankDefinitionId = lifeRank,
                RankName = rankNames.TryGetValue(lifeRank, out var rn) ? rn : string.Empty,
                ConsecutiveDays = streak,
                StreakStartDate = prevDay,
                StreakEndDate = mostRecent.Day,
                BonusAmount = typeByRank.TryGetValue(lifeRank, out var t) ? t.Amount : 0m
            });
        }

        var total = rows.Count;
        var ordered = rows.OrderByDescending(x => x.ConsecutiveDays).ThenBy(x => x.MemberId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToList();

        // Page-only name enrichment.
        var pageIds = ordered.Select(x => x.MemberId).ToList();
        var names = await _db.MemberProfiles.AsNoTracking()
            .Where(m => pageIds.Contains(m.MemberId))
            .Select(m => new { m.MemberId, FullName = (m.FirstName + " " + m.LastName).Trim() })
            .ToDictionaryAsync(m => m.MemberId, m => m.FullName, ct);
        foreach (var row in ordered)
            row.FullName = names.TryGetValue(row.MemberId, out var n) ? n : string.Empty;

        return Result<PagedResult<RankSeniorityRowDto>>.Success(new PagedResult<RankSeniorityRowDto>
        { Items = ordered, TotalCount = total, Page = page, PageSize = pageSize });
    }
}
