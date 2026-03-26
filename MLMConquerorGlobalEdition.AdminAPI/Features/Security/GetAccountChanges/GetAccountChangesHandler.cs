using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Security.GetAccountChanges;

public class GetAccountChangesHandler : IRequestHandler<GetAccountChangesQuery, Result<PagedResult<AdminMemberDto>>>
{
    private readonly AppDbContext _db;

    public GetAccountChangesHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<AdminMemberDto>>> Handle(
        GetAccountChangesQuery request, CancellationToken cancellationToken)
    {
        var query = _db.MemberProfiles.AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(m => m.LastUpdateDate)
            .Skip((request.Page.Page - 1) * request.Page.PageSize)
            .Take(request.Page.PageSize)
            .Select(m => new AdminMemberDto
            {
                MemberId = m.MemberId,
                FirstName = m.FirstName,
                LastName = m.LastName,
                Phone = m.Phone,
                Country = m.Country,
                Status = m.Status.ToString(),
                MemberType = m.MemberType.ToString(),
                EnrollDate = m.EnrollDate,
                SponsorMemberId = m.SponsorMemberId,
                CreationDate = m.CreationDate
            })
            .ToListAsync(cancellationToken);

        var result = new PagedResult<AdminMemberDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page.Page,
            PageSize = request.Page.PageSize
        };

        return Result<PagedResult<AdminMemberDto>>.Success(result);
    }
}
