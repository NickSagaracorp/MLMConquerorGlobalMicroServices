using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.SubmitCsat;

public class SubmitCsatHandler : IRequestHandler<SubmitCsatCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public SubmitCsatHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<bool>> Handle(SubmitCsatCommand request, CancellationToken ct)
    {
        if (request.Request.Score is < 1 or > 5)
            return Result<bool>.Failure("CSAT_SCORE_INVALID", "Score must be between 1 and 5.");

        var ticket = await _db.Tickets
            .Where(t => t.Id == request.TicketId && !t.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (ticket is null)
            return Result<bool>.Failure("TICKET_NOT_FOUND", "Ticket not found.");

        if (ticket.MemberId != _currentUser.MemberId)
            return Result<bool>.Failure("FORBIDDEN", "Only the ticket owner can submit CSAT.");

        if (ticket.Status != TicketStatus.Resolved && ticket.Status != TicketStatus.Closed)
            return Result<bool>.Failure("TICKET_NOT_RESOLVED", "CSAT can only be submitted after the ticket is resolved or closed.");

        if (ticket.CsatScore.HasValue)
            return Result<bool>.Failure("CSAT_ALREADY_SUBMITTED", "CSAT has already been submitted for this ticket.");

        var now = _dateTime.Now;
        ticket.CsatScore        = request.Request.Score;
        ticket.CsatComment      = request.Request.Comment;
        ticket.CsatSubmittedAt  = now;
        ticket.LastUpdateDate   = now;
        ticket.LastUpdateBy     = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
