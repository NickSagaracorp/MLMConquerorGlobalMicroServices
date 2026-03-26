using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.ResolveTicket;

public class ResolveTicketHandler : IRequestHandler<ResolveTicketCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public ResolveTicketHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<bool>> Handle(ResolveTicketCommand request, CancellationToken ct)
    {
        var ticket = await _db.Tickets
            .Where(t => t.Id == request.TicketId && !t.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (ticket is null)
            return Result<bool>.Failure("TICKET_NOT_FOUND", "Ticket not found.");

        // Members can only resolve their own tickets; admins can resolve any
        if (!_currentUser.IsAdmin && ticket.MemberId != _currentUser.MemberId)
            return Result<bool>.Failure("FORBIDDEN", "Access denied.");

        var now = _dateTime.Now;

        ticket.Status = TicketStatus.Resolved;
        ticket.ResolvedAt = now;
        ticket.LastUpdateDate = now;
        ticket.LastUpdateBy = _currentUser.UserId;

        // Add a resolution comment if notes were provided
        if (!string.IsNullOrWhiteSpace(request.Request.ResolutionNotes))
        {
            var comment = new TicketComment
            {
                TicketId = ticket.Id,
                AuthorId = _currentUser.UserId,
                Body = request.Request.ResolutionNotes,
                IsInternal = false,
                CreationDate = now,
                CreatedBy = _currentUser.UserId
            };

            await _db.TicketComments.AddAsync(comment, ct);
        }

        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
