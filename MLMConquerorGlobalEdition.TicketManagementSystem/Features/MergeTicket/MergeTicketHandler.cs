using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.MergeTicket;

public class MergeTicketHandler : IRequestHandler<MergeTicketCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public MergeTicketHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<bool>> Handle(MergeTicketCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin)
            return Result<bool>.Failure("FORBIDDEN", "Admin access required.");

        if (string.Equals(request.TicketId, request.Request.TargetTicketId, StringComparison.OrdinalIgnoreCase))
            return Result<bool>.Failure("MERGE_SAME_TICKET", "Cannot merge a ticket into itself.");

        var source = await _db.Tickets
            .Where(t => t.Id == request.TicketId && !t.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (source is null)
            return Result<bool>.Failure("TICKET_NOT_FOUND", "Source ticket not found.");

        var target = await _db.Tickets
            .Where(t => t.Id == request.Request.TargetTicketId && !t.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (target is null)
            return Result<bool>.Failure("TARGET_TICKET_NOT_FOUND", "Target ticket not found.");

        var now = _dateTime.Now;

        source.MergedIntoTicketId = target.Id;
        source.Status = TicketStatus.Closed;
        source.LastUpdateDate = now;
        source.LastUpdateBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
