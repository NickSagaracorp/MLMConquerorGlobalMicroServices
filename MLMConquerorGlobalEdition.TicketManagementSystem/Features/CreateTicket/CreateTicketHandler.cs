using MediatR;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.CreateTicket;

public class CreateTicketHandler : IRequestHandler<CreateTicketCommand, Result<TicketDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CreateTicketHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<TicketDto>> Handle(CreateTicketCommand request, CancellationToken ct)
    {
        var now = _dateTime.Now;

        var ticket = new Ticket
        {
            MemberId = _currentUser.MemberId,
            Subject = request.Request.Subject,
            Body = request.Request.Body,
            CategoryId = request.Request.CategoryId,
            Priority = request.Request.Priority,
            Status = TicketStatus.Open,
            CreationDate = now,
            CreatedBy = _currentUser.UserId,
            LastUpdateDate = now,
            LastUpdateBy = _currentUser.UserId
        };

        await _db.Tickets.AddAsync(ticket, ct);
        await _db.SaveChangesAsync(ct);

        var dto = new TicketDto
        {
            Id = ticket.Id,
            Subject = ticket.Subject,
            Body = ticket.Body,
            Status = ticket.Status.ToString(),
            Priority = ticket.Priority.ToString(),
            CategoryId = ticket.CategoryId,
            CategoryName = null,
            MemberId = ticket.MemberId,
            AssignedToUserId = ticket.AssignedToUserId,
            ResolvedAt = ticket.ResolvedAt,
            CreationDate = ticket.CreationDate,
            CommentCount = 0
        };

        return Result<TicketDto>.Success(dto);
    }
}
