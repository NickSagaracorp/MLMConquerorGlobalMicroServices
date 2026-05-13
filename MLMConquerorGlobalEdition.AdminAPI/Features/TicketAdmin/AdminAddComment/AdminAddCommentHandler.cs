using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminAddComment;

public class AdminAddCommentHandler : IRequestHandler<AdminAddCommentCommand, Result<AdminTicketCommentDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public AdminAddCommentHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
    }

    public async Task<Result<AdminTicketCommentDto>> Handle(
        AdminAddCommentCommand command, CancellationToken ct)
    {
        var now = _dateTime.Now;

        var ticket = await _db.Tickets
            .FirstOrDefaultAsync(t => t.Id == command.TicketId && !t.IsDeleted, ct);

        if (ticket is null)
            return Result<AdminTicketCommentDto>.Failure("TICKET_NOT_FOUND", "Ticket not found.");

        var comment = new TicketComment
        {
            TicketId     = command.TicketId,
            AuthorId     = _currentUser.UserId,
            AuthorType   = "agent",
            Body         = command.Request.Content,
            IsInternal   = command.Request.IsInternal,
            CreatedBy    = _currentUser.UserId,
            CreationDate = now
        };

        _db.TicketComments.Add(comment);

        ticket.LastUpdateDate = now;
        ticket.LastUpdateBy   = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        // Push notifications for staff replies are produced by the BizCenter pipeline
        // when staff comment via that surface. AdminAPI does not depend on Firebase.
        var dto = new AdminTicketCommentDto
        {
            Id           = comment.Id,
            AuthorId     = comment.AuthorId,
            Author       = "Support",
            Body         = comment.Body,
            IsStaff      = true,
            CreationDate = comment.CreationDate
        };

        return Result<AdminTicketCommentDto>.Success(dto);
    }
}
