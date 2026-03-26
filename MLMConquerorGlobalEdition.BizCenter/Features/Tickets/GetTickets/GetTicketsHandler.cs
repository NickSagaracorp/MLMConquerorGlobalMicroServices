using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tickets.GetTickets;

public class GetTicketsHandler : IRequestHandler<GetTicketsQuery, Result<PagedResult<TicketDto>>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetTicketsHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<TicketDto>>> Handle(GetTicketsQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        var query = _db.Tickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Where(t => t.MemberId == memberId && !t.IsDeleted)
            .OrderByDescending(t => t.CreationDate);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new TicketDto
            {
                Id = t.Id,
                Subject = t.Subject,
                Body = t.Body,
                Status = t.Status.ToString(),
                Priority = t.Priority.ToString(),
                CategoryName = t.Category != null ? t.Category.Name : string.Empty,
                CreationDate = t.CreationDate,
                AssignedToUserId = t.AssignedToUserId
            })
            .ToListAsync(ct);

        var result = new PagedResult<TicketDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Result<PagedResult<TicketDto>>.Success(result);
    }
}
