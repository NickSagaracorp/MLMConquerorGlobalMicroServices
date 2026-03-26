using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminCreateTicket;

public class AdminCreateTicketHandler : IRequestHandler<AdminCreateTicketCommand, Result<AdminTicketDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public AdminCreateTicketHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<AdminTicketDto>> Handle(
        AdminCreateTicketCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        var now = _dateTime.Now;

        var ticket = new Ticket
        {
            MemberId = req.MemberId,
            Subject = req.Subject,
            Body = req.Body,
            CategoryId = req.CategoryId,
            Priority = req.Priority,
            Status = TicketStatus.Open,
            CreationDate = now,
            CreatedBy = _currentUser.UserId,
            LastUpdateDate = now,
            LastUpdateBy = _currentUser.UserId
        };

        await _db.Tickets.AddAsync(ticket, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<AdminTicketDto>.Success(new AdminTicketDto
        {
            Id = ticket.Id,
            Subject = ticket.Subject,
            MemberId = ticket.MemberId,
            Status = ticket.Status.ToString(),
            Priority = ticket.Priority.ToString(),
            CreationDate = ticket.CreationDate,
            CommentCount = 0
        });
    }
}
