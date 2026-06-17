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

        // JOIN the genealogy subtree to profiles and page at the DB — instead of loading ALL
        // ~120k descendant ids into memory and re-querying with Contains(allIds). Only the
        // requested page ever materializes.
        var query =
            from g in _db.GenealogyTree.AsNoTracking()
            where g.HierarchyPath.Contains(hierarchySearchPattern)
            join m in _db.MemberProfiles.AsNoTracking() on g.MemberId equals m.MemberId
            orderby m.EnrollDate descending
            select m;

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
