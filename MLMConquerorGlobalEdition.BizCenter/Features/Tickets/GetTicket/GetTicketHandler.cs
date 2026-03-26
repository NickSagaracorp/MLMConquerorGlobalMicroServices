using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tickets.GetTicket;

public class GetTicketHandler : IRequestHandler<GetTicketQuery, Result<TicketDetailDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetTicketHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<TicketDetailDto>> Handle(GetTicketQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        var ticket = await _db.Tickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId && t.MemberId == memberId && !t.IsDeleted, ct);

        if (ticket is null)
            return Result<TicketDetailDto>.Failure("TICKET_NOT_FOUND", "Ticket not found.");

        var dto = new TicketDetailDto
        {
            Id = ticket.Id,
            Subject = ticket.Subject,
            Body = ticket.Body,
            Status = ticket.Status.ToString(),
            Priority = ticket.Priority.ToString(),
            CategoryName = ticket.Category?.Name ?? string.Empty,
            CreationDate = ticket.CreationDate,
            AssignedToUserId = ticket.AssignedToUserId,
            Comments = ticket.Comments
                .OrderBy(c => c.CreationDate)
                .Select(c => new TicketCommentDto
                {
                    Id = c.Id,
                    AuthorId = c.AuthorId,
                    Body = c.Body,
                    CreationDate = c.CreationDate
                })
                .ToList()
        };

        return Result<TicketDetailDto>.Success(dto);
    }
}
