using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.AddComment;

public class AddCommentHandler : IRequestHandler<AddCommentCommand, Result<TicketCommentDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public AddCommentHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<TicketCommentDto>> Handle(AddCommentCommand request, CancellationToken ct)
    {
        var ticket = await _db.Tickets
            .Where(t => t.Id == request.TicketId && !t.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (ticket is null)
            return Result<TicketCommentDto>.Failure("TICKET_NOT_FOUND", "Ticket not found.");

        if (!_currentUser.IsAdmin && ticket.MemberId != _currentUser.MemberId)
            return Result<TicketCommentDto>.Failure("FORBIDDEN", "Access denied.");

        // Only admins can post internal comments
        var isInternal = request.Request.IsInternal && _currentUser.IsAdmin;

        var now = _dateTime.Now;

        var comment = new TicketComment
        {
            TicketId = ticket.Id,
            AuthorId = _currentUser.UserId,
            Body = request.Request.Content,
            IsInternal = isInternal,
            CreationDate = now,
            CreatedBy = _currentUser.UserId
        };

        await _db.TicketComments.AddAsync(comment, ct);

        ticket.LastUpdateDate = now;
        ticket.LastUpdateBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        var dto = new TicketCommentDto
        {
            Id = comment.Id,
            TicketId = comment.TicketId,
            AuthorId = comment.AuthorId,
            Content = comment.Body,
            IsInternal = comment.IsInternal,
            CreationDate = comment.CreationDate
        };

        return Result<TicketCommentDto>.Success(dto);
    }
}
