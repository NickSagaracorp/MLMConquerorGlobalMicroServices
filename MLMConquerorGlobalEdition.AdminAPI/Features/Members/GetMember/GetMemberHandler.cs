using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Members.GetMember;

public class GetMemberHandler : IRequestHandler<GetMemberQuery, Result<AdminMemberDetailDto>>
{
    private readonly AppDbContext _db;

    public GetMemberHandler(AppDbContext db) => _db = db;

    public async Task<Result<AdminMemberDetailDto>> Handle(
        GetMemberQuery request, CancellationToken cancellationToken)
    {
        var member = await _db.MemberProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == request.MemberId, cancellationToken);

        if (member is null)
            return Result<AdminMemberDetailDto>.Failure("MEMBER_NOT_FOUND", $"Member '{request.MemberId}' not found.");

        var stats = await _db.MemberStatistics
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.MemberId == request.MemberId, cancellationToken);

        var allRankHistory = await _db.MemberRankHistories
            .AsNoTracking()
            .Where(r => r.MemberId == request.MemberId)
            .Join(_db.RankDefinitions,
                  h => h.RankDefinitionId,
                  d => d.Id,
                  (h, d) => new { h.RankDefinitionId, d.Name, d.SortOrder, h.AchievedAt })
            .ToListAsync(cancellationToken);

        var currentRank  = allRankHistory.OrderByDescending(r => r.AchievedAt).FirstOrDefault();
        var lifetimeRank = allRankHistory.OrderByDescending(r => r.SortOrder).FirstOrDefault();

        // Default to first rank (lowest SortOrder) when member has no rank history
        string? currentRankName  = currentRank?.Name;
        int?    currentRankId    = currentRank?.RankDefinitionId;
        string? lifetimeRankName = lifetimeRank?.Name;
        int?    lifetimeRankId   = lifetimeRank?.RankDefinitionId;

        if (currentRank is null)
        {
            var defaultRank = await _db.RankDefinitions
                .AsNoTracking()
                .OrderBy(r => r.SortOrder)
                .Select(r => new { r.Id, r.Name })
                .FirstOrDefaultAsync(cancellationToken);

            currentRankName  = defaultRank?.Name;
            currentRankId    = defaultRank?.Id;
            lifetimeRankName = defaultRank?.Name;
            lifetimeRankId   = defaultRank?.Id;
        }

        var dto = new AdminMemberDetailDto
        {
            MemberId = member.MemberId,
            FirstName = member.FirstName,
            LastName = member.LastName,
            Phone = member.Phone,
            Country = member.Country,
            Status = member.Status.ToString(),
            MemberType = member.MemberType.ToString(),
            EnrollDate = member.EnrollDate,
            SponsorMemberId = member.SponsorMemberId,
            CreationDate = member.CreationDate,
            DualTeamPoints = stats?.DualTeamPoints ?? 0,
            EnrollmentPoints = stats?.EnrollmentPoints ?? 0,
            DualTeamSize = stats?.DualTeamSize ?? 0,
            EnrollmentTeamSize = stats?.EnrollmentTeamSize ?? 0,
            CurrentMonthIncome = stats?.CurrentMonthIncomeGrowth ?? 0,
            CurrentYearIncome = stats?.CurrentYearIncomeGrowth ?? 0,
            CurrentRank    = currentRankName,
            CurrentRankId  = currentRankId,
            LifetimeRank   = lifetimeRankName,
            LifetimeRankId = lifetimeRankId
        };

        return Result<AdminMemberDetailDto>.Success(dto);
    }
}
