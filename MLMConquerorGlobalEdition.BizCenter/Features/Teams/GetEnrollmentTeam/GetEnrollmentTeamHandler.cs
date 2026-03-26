using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetEnrollmentTeam;

public class GetEnrollmentTeamHandler : IRequestHandler<GetEnrollmentTeamQuery, Result<IEnumerable<TeamMemberDto>>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetEnrollmentTeamHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<IEnumerable<TeamMemberDto>>> Handle(GetEnrollmentTeamQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        // Direct children in genealogy tree (Level 1 downline)
        var childMemberIds = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => g.ParentMemberId == memberId)
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        var members = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => childMemberIds.Contains(m.MemberId))
            .Select(m => new TeamMemberDto
            {
                MemberId = m.MemberId,
                FirstName = m.FirstName,
                LastName = m.LastName,
                MemberType = m.MemberType.ToString(),
                Status = m.Status.ToString(),
                EnrollDate = m.EnrollDate,
                SponsorMemberId = m.SponsorMemberId
            })
            .ToListAsync(ct);

        return Result<IEnumerable<TeamMemberDto>>.Success(members);
    }
}
