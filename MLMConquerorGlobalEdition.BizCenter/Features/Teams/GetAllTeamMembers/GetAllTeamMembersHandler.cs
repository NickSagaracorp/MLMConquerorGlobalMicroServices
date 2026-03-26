using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetAllTeamMembers;

public class GetAllTeamMembersHandler : IRequestHandler<GetAllTeamMembersQuery, Result<PagedResult<TeamMemberDto>>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetAllTeamMembersHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<TeamMemberDto>>> Handle(GetAllTeamMembersQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var hierarchySearchPattern = "/" + memberId + "/";

        // Full subtree via materialized path — all descendants
        var subtreeMemberIds = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => g.HierarchyPath.Contains(hierarchySearchPattern))
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        var query = _db.MemberProfiles
            .AsNoTracking()
            .Where(m => subtreeMemberIds.Contains(m.MemberId))
            .OrderByDescending(m => m.EnrollDate);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
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

        var result = new PagedResult<TeamMemberDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Result<PagedResult<TeamMemberDto>>.Success(result);
    }
}
