using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tickets.AddTicketComment;

public class AddTicketCommentHandler : IRequestHandler<AddTicketCommentCommand, Result<TicketCommentDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly IPushNotificationService _push;

    public AddTicketCommentHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime,
        IPushNotificationService push)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
        _push        = push;
    }

    public async Task<Result<TicketCommentDto>> Handle(AddTicketCommentCommand command, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var now      = _dateTime.UtcNow;

        var ticket = await _db.Tickets
            .FirstOrDefaultAsync(t => t.Id == command.TicketId && t.MemberId == memberId && !t.IsDeleted, ct);

        if (ticket is null)
            return Result<TicketCommentDto>.Failure("TICKET_NOT_FOUND", "Ticket not found.");

        var comment = new TicketComment
        {
            TicketId     = command.TicketId,
            AuthorId     = _currentUser.UserId,
            Body         = command.Request.Content,
            IsInternal   = false,
            CreatedBy    = _currentUser.UserId,
            CreationDate = now
        };

        _db.TicketComments.Add(comment);

        ticket.LastUpdateDate = now;
        ticket.LastUpdateBy   = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        // Send push notification to the ticket owner when an admin replies on their behalf
        // (e.g. during impersonation) or when staff respond via the same endpoint
        if (_currentUser.IsAdmin && ticket.MemberId != memberId)
        {
            await _push.SendAsync(
                memberId:  ticket.MemberId,
                eventType: "ticket_comment",
                title:     "Your ticket has a new reply",
                body:      $"Support has replied to ticket #{ticket.Id}.",
                ct:        ct);
        }

        var dto = new TicketCommentDto
        {
            Id           = comment.Id,
            AuthorId     = comment.AuthorId,
            Body         = comment.Body,
            CreationDate = comment.CreationDate
        };

        return Result<TicketCommentDto>.Success(dto);
    }
}
