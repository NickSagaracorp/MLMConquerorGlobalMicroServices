using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetTeamMembers;

public class GetTeamMembersHandler : IRequestHandler<GetTeamMembersQuery, Result<PagedResult<TeamMemberDto>>>
{
    private readonly AppDbContext            _db;
    private readonly ICurrentUserService     _currentUser;
    private readonly IRankComputationService _ranks;

    public GetTeamMembersHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IRankComputationService ranks)
    {
        _db          = db;
        _currentUser = currentUser;
        _ranks       = ranks;
    }

    public async Task<Result<PagedResult<TeamMemberDto>>> Handle(GetTeamMembersQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        // Direct sponsored members (SponsorMemberId == current member). Page
        // first so the per-row leg-cap / leg-side joins only run on the page
        // we actually return.
        var query = _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.SponsorMemberId == memberId)
            .OrderByDescending(m => m.EnrollDate);

        var totalCount = await query.CountAsync(ct);

        var pageProfiles = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new
            {
                m.MemberId, m.FirstName, m.LastName, m.MemberType, m.Status,
                m.EnrollDate, m.SponsorMemberId
            })
            .ToListAsync(ct);

        var pageIds = pageProfiles.Select(p => p.MemberId).ToList();

        // Stats (PersonalPoints) and binary tree position (Leg).
        var statsMap = await _db.MemberStatistics.AsNoTracking()
            .Where(s => pageIds.Contains(s.MemberId))
            .ToDictionaryAsync(s => s.MemberId, ct);

        var dualNodes = await _db.DualTeamTree.AsNoTracking()
            .Where(d => pageIds.Contains(d.MemberId))
            .Select(d => new { d.MemberId, d.Side })
            .ToListAsync(ct);
        var dualSideMap = dualNodes.ToDictionary(d => d.MemberId, d => d.Side);

        // Per-leg DT cap for the viewer's current and next rank. Same rule the
        // qualification engine uses (MaxTeamPointsPerBranch * TeamPoints). cap
        // = 0 means DT does not apply at this rank — the client renders "—".
        var summary = await _ranks.GetSummaryAsync(memberId, ct);
        var rankReqs = await _db.RankDefinitions.AsNoTracking()
            .Include(r => r.Requirements)
            .Where(r => r.SortOrder == summary.CurrentRankSortOrder
                     || r.SortOrder == summary.NextRankSortOrder)
            .ToListAsync(ct);

        var currentReq = rankReqs.FirstOrDefault(r => r.SortOrder == summary.CurrentRankSortOrder)
            ?.Requirements.OrderBy(r => r.LevelNo).FirstOrDefault();
        var nextReq    = rankReqs.FirstOrDefault(r => r.SortOrder == summary.NextRankSortOrder)
            ?.Requirements.OrderBy(r => r.LevelNo).FirstOrDefault();

        static int CalcLegCap(RankRequirement? req) =>
            req is { TeamPoints: > 0, MaxTeamPointsPerBranch: > 0 }
                ? (int)Math.Round(req.MaxTeamPointsPerBranch * req.TeamPoints)
                : 0;

        var legCapCurrent = CalcLegCap(currentReq);
        var legCapNext    = CalcLegCap(nextReq);

        var items = pageProfiles.Select(p =>
        {
            statsMap.TryGetValue(p.MemberId, out var stat);
            var personalPts        = stat?.PersonalPoints ?? 0;
            var eligibleCurrentPts = legCapCurrent > 0 ? Math.Min(personalPts, legCapCurrent) : 0;
            var eligibleNextPts    = legCapNext    > 0 ? Math.Min(personalPts, legCapNext)    : 0;
            var leg = dualSideMap.TryGetValue(p.MemberId, out var side)
                ? (side == TreeSide.Left ? "Left" : "Right")
                : string.Empty;

            return new TeamMemberDto
            {
                MemberId                  = p.MemberId,
                FullName                  = $"{p.FirstName} {p.LastName}".Trim(),
                FirstName                 = p.FirstName,
                LastName                  = p.LastName,
                MemberType                = p.MemberType.ToString(),
                Status                    = p.Status.ToString(),
                EnrollDate                = p.EnrollDate,
                SponsorMemberId           = p.SponsorMemberId,
                Leg                       = leg,
                QualificationPoints       = personalPts,
                CurrentRankEligiblePoints = eligibleCurrentPts,
                CurrentRankEligiblePct    = legCapCurrent > 0
                    ? Math.Min(100, eligibleCurrentPts * 100 / legCapCurrent) : 0,
                NextRankEligiblePoints    = eligibleNextPts,
                NextRankEligiblePct       = legCapNext > 0
                    ? Math.Min(100, eligibleNextPts * 100 / legCapNext) : 0
            };
        }).ToList();

        var result = new PagedResult<TeamMemberDto>
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = request.Page,
            PageSize   = request.PageSize
        };

        return Result<PagedResult<TeamMemberDto>>.Success(result);
    }
}
