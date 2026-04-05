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
    private readonly ISlaMonitorService _sla;

    public AddCommentHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime,
        ISlaMonitorService sla)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
        _sla = sla;
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

        var isInternal  = request.Request.IsInternal && _currentUser.IsAdmin;
        var isAgent     = _currentUser.IsAdmin || _currentUser.Roles.Contains("Agent");
        var authorType  = isAgent ? "agent" : "customer";

        var now = _dateTime.Now;

        var comment = new TicketComment
        {
            TicketId     = ticket.Id,
            AuthorId     = _currentUser.UserId,
            AuthorType   = authorType,
            Body         = request.Request.Content,
            IsInternal   = isInternal,
            CreationDate = now,
            CreatedBy    = _currentUser.UserId
        };

        await _db.TicketComments.AddAsync(comment, ct);

        // ── Auto-status rules ─────────────────────────────────────────────────
        var oldStatus = ticket.Status;

        if (authorType == "agent" && ticket.Status == TicketStatus.InProgress)
        {
            ticket.Status = TicketStatus.WaitingForUser;

            // Pause SLA timer
            if (ticket.SlaPolicyId is not null)
                _sla.PauseTimer(ticket, now);

            _db.TicketHistories.Add(BuildHistory(ticket.Id, "status", oldStatus.ToString(), ticket.Status.ToString(), "system", now));
        }
        else if (authorType == "customer" && ticket.Status == TicketStatus.WaitingForUser)
        {
            ticket.Status = TicketStatus.InProgress;

            // Record first response time
            if (ticket.SlaFirstResponseAt is null)
                ticket.SlaFirstResponseAt = now;

            // Resume SLA timer
            if (ticket.SlaPolicyId is not null)
                _sla.ResumeTimer(ticket, now);

            _db.TicketHistories.Add(BuildHistory(ticket.Id, "status", oldStatus.ToString(), ticket.Status.ToString(), "customer", now));
        }

        ticket.LastUpdateDate = now;
        ticket.LastUpdateBy   = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        return Result<TicketCommentDto>.Success(new TicketCommentDto
        {
            Id           = comment.Id,
            TicketId     = comment.TicketId,
            AuthorId     = comment.AuthorId,
            AuthorType   = comment.AuthorType,
            Content      = comment.Body,
            IsInternal   = comment.IsInternal,
            CreationDate = comment.CreationDate
        });
    }

    private static TicketHistory BuildHistory(
        string ticketId, string field, string? oldVal, string? newVal, string byType, DateTime now) =>
        new()
        {
            TicketId      = ticketId,
            Field         = field,
            OldValue      = oldVal,
            NewValue      = newVal,
            ChangedByType = byType,
            CreationDate  = now,
            CreatedBy     = byType
        };
}
