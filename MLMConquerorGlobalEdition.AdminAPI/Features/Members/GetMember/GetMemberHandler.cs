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
            CurrentYearIncome = stats?.CurrentYearIncomeGrowth ?? 0
        };

        return Result<AdminMemberDetailDto>.Success(dto);
    }
}
