using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Members.GetMembers;

public class GetMembersHandler : IRequestHandler<GetMembersQuery, Result<PagedResult<AdminMemberDto>>>
{
    private readonly AppDbContext _db;

    public GetMembersHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<AdminMemberDto>>> Handle(
        GetMembersQuery request, CancellationToken cancellationToken)
    {
        var query = _db.MemberProfiles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.StatusFilter) &&
            Enum.TryParse<MemberAccountStatus>(request.StatusFilter, true, out var statusEnum))
        {
            query = query.Where(m => m.Status == statusEnum);
        }

        if (!string.IsNullOrWhiteSpace(request.SponsorId))
        {
            query = query.Where(m => m.SponsorMemberId == request.SponsorId);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            query = query.Where(m => m.FirstName.ToLower().Contains(term) ||
                                      m.LastName.ToLower().Contains(term) ||
                                      m.MemberId.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(m => m.CreationDate)
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
