using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Members.GetMember;

public class GetMemberHandler : IRequestHandler<GetMemberQuery, Result<AdminMemberDetailDto>>
{
    private readonly AppDbContext            _db;
    private readonly IRankComputationService _ranks;

    public GetMemberHandler(AppDbContext db, IRankComputationService ranks)
    {
        _db    = db;
        _ranks = ranks;
    }

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

        // Resolve sponsor full name (one extra lookup; SponsorMemberId may be null for the root).
        string? sponsorFullName = null;
        if (!string.IsNullOrEmpty(member.SponsorMemberId))
        {
            sponsorFullName = await _db.MemberProfiles
                .AsNoTracking()
                .Where(m => m.MemberId == member.SponsorMemberId)
                .Select(m => (m.FirstName + " " + m.LastName).Trim())
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Resolve dual-team upline (the binary-tree parent — different from the enrollment sponsor).
        string? dualUplineMemberId = await _db.DualTeamTree
            .AsNoTracking()
            .Where(d => d.MemberId == request.MemberId)
            .Select(d => d.ParentMemberId)
            .FirstOrDefaultAsync(cancellationToken);

        string? dualUplineFullName = null;
        if (!string.IsNullOrEmpty(dualUplineMemberId))
        {
            dualUplineFullName = await _db.MemberProfiles
                .AsNoTracking()
                .Where(m => m.MemberId == dualUplineMemberId)
                .Select(m => (m.FirstName + " " + m.LastName).Trim())
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Current rank = LIVE qualification (capped per-leg / per-branch). Lifetime
        // rank = highest SortOrder ever achieved. Both come from the shared service
        // so this view never drifts from Residuals/Branches/RankEngine.
        var summary = await _ranks.GetSummaryAsync(request.MemberId, cancellationToken);

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
            SponsorFullName = sponsorFullName,
            DualTeamUplineMemberId = dualUplineMemberId,
            DualTeamUplineFullName = dualUplineFullName,
            CreationDate = member.CreationDate,
            DualTeamPoints = stats?.DualTeamPoints ?? 0,
            EnrollmentPoints = stats?.EnrollmentPoints ?? 0,
            DualTeamSize = stats?.DualTeamSize ?? 0,
            EnrollmentTeamSize = stats?.EnrollmentTeamSize ?? 0,
            CurrentMonthIncome = stats?.CurrentMonthIncomeGrowth ?? 0,
            CurrentYearIncome = stats?.CurrentYearIncomeGrowth ?? 0,
            CurrentRank    = summary.CurrentRankName,
            CurrentRankId  = summary.CurrentRankId,
            LifetimeRank   = summary.LifetimeRankName,
            LifetimeRankId = summary.LifetimeRankId
        };

        return Result<AdminMemberDetailDto>.Success(dto);
    }
}
