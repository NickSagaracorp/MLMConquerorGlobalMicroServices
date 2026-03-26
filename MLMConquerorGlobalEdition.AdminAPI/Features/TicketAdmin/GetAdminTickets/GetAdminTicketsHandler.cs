using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.GetAdminTickets;

public class GetAdminTicketsHandler : IRequestHandler<GetAdminTicketsQuery, Result<PagedResult<AdminTicketDto>>>
{
    private readonly AppDbContext _db;

    public GetAdminTicketsHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<AdminTicketDto>>> Handle(
        GetAdminTicketsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.Tickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Comments);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.CreationDate)
            .Skip((request.Page.Page - 1) * request.Page.PageSize)
            .Take(request.Page.PageSize)
            .Select(t => new AdminTicketDto
            {
                Id = t.Id,
                Subject = t.Subject,
                MemberId = t.MemberId,
                Status = t.Status.ToString(),
                Priority = t.Priority.ToString(),
                CategoryName = t.Category != null ? t.Category.Name : null,
                AssignedToUserId = t.AssignedToUserId,
                CreationDate = t.CreationDate,
                CommentCount = t.Comments.Count
            })
            .ToListAsync(cancellationToken);

        var result = new PagedResult<AdminTicketDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page.Page,
            PageSize = request.Page.PageSize
        };

        return Result<PagedResult<AdminTicketDto>>.Success(result);
    }
}
