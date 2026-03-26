using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tickets.AddTicketComment;

public class AddTicketCommentHandler : IRequestHandler<AddTicketCommentCommand, Result<TicketCommentDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public AddTicketCommentHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<TicketCommentDto>> Handle(AddTicketCommentCommand command, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var now = _dateTime.UtcNow;

        var ticket = await _db.Tickets
            .FirstOrDefaultAsync(t => t.Id == command.TicketId && t.MemberId == memberId && !t.IsDeleted, ct);

        if (ticket is null)
            return Result<TicketCommentDto>.Failure("TICKET_NOT_FOUND", "Ticket not found.");

        var comment = new TicketComment
        {
            TicketId = command.TicketId,
            AuthorId = _currentUser.UserId,
            Body = command.Request.Content,
            IsInternal = false,
            CreatedBy = _currentUser.UserId,
            CreationDate = now
        };

        _db.TicketComments.Add(comment);

        ticket.LastUpdateDate = now;
        ticket.LastUpdateBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        var dto = new TicketCommentDto
        {
            Id = comment.Id,
            AuthorId = comment.AuthorId,
            Body = comment.Body,
            CreationDate = comment.CreationDate
        };

        return Result<TicketCommentDto>.Success(dto);
    }
}
