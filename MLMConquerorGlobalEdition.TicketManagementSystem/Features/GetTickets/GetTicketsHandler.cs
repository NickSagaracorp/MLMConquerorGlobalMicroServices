using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.GetTickets;

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
        var query = _db.Tickets
            .AsNoTracking()
            .Where(t => !t.IsDeleted);

        if (!_currentUser.IsAdmin)
            query = query.Where(t => t.MemberId == _currentUser.MemberId);

        if (!string.IsNullOrWhiteSpace(request.StatusFilter) &&
            Enum.TryParse<TicketStatus>(request.StatusFilter, ignoreCase: true, out var statusEnum))
        {
            query = query.Where(t => t.Status == statusEnum);
        }

        var totalCount = await query.CountAsync(ct);

        var page = request.Page.Page < 1 ? 1 : request.Page.Page;
        var pageSize = request.Page.PageSize < 1 ? 20 : request.Page.PageSize;

        var items = await query
            .OrderByDescending(t => t.CreationDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TicketDto
            {
                Id = t.Id,
                Subject = t.Subject,
                Body = t.Body,
                Status = t.Status.ToString(),
                Priority = t.Priority.ToString(),
                CategoryId = t.CategoryId,
                CategoryName = t.Category != null ? t.Category.Name : null,
                MemberId = t.MemberId,
                AssignedToUserId = t.AssignedToUserId,
                ResolvedAt = t.ResolvedAt,
                CreationDate = t.CreationDate,
                CommentCount = t.Comments.Count
            })
            .ToListAsync(ct);

        var result = new PagedResult<TicketDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Result<PagedResult<TicketDto>>.Success(result);
    }
}
