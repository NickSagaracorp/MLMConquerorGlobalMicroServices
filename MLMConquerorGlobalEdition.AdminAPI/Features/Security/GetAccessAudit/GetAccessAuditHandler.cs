using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Security.GetAccessAudit;

public class GetAccessAuditHandler : IRequestHandler<GetAccessAuditQuery, Result<PagedResult<MemberStatusHistory>>>
{
    private readonly AppDbContext _db;

    public GetAccessAuditHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<MemberStatusHistory>>> Handle(
        GetAccessAuditQuery request, CancellationToken cancellationToken)
    {
        var query = _db.MemberStatusHistories.AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(h => h.ChangedAt)
            .Skip((request.Page.Page - 1) * request.Page.PageSize)
            .Take(request.Page.PageSize)
            .ToListAsync(cancellationToken);

        var result = new PagedResult<MemberStatusHistory>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page.Page,
            PageSize = request.Page.PageSize
        };

        return Result<PagedResult<MemberStatusHistory>>.Success(result);
    }
}
