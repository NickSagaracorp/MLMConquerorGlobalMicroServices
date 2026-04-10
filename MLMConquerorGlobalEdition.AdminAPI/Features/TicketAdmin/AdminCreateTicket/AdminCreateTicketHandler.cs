using MediatR;
using Microsoft.EntityFrameworkCore;
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

        // Resolve category — fall back to first active category when not specified
        int resolvedCategoryId;
        if (req.CategoryId is > 0)
        {
            resolvedCategoryId = req.CategoryId.Value;
        }
        else
        {
            var defaultCat = await _db.TicketCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (defaultCat is null)
                return Result<AdminTicketDto>.Failure("NO_CATEGORY", "No active ticket category found. Please create one first.");

            resolvedCategoryId = defaultCat.Id;
        }

        var ticketNumber = await GenerateTicketNumberAsync(now, cancellationToken);

        var ticket = new Ticket
        {
            TicketNumber   = ticketNumber,
            MemberId       = req.MemberId,
            Subject        = req.Subject,
            Body           = req.Body ?? string.Empty,
            CategoryId     = resolvedCategoryId,
            Priority       = req.Priority,
            Channel        = TicketChannel.Portal,
            Status         = TicketStatus.Open,
            CreationDate   = now,
            CreatedBy      = _currentUser.UserId,
            LastUpdateDate = now,
            LastUpdateBy   = _currentUser.UserId
        };

        await _db.Tickets.AddAsync(ticket, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var categoryName = (await _db.TicketCategories.FindAsync([resolvedCategoryId], cancellationToken))?.Name;

        return Result<AdminTicketDto>.Success(new AdminTicketDto
        {
            Id           = ticket.Id,
            Subject      = ticket.Subject,
            MemberId     = ticket.MemberId,
            Status       = ticket.Status.ToString(),
            Priority     = ticket.Priority.ToString(),
            CategoryName = categoryName,
            CreationDate = ticket.CreationDate,
            CommentCount = 0
        });
    }

    private async Task<string> GenerateTicketNumberAsync(DateTime now, CancellationToken ct)
    {
        var today = now.Date;

        var seq = await _db.TicketSequences.FindAsync([today], ct);
        if (seq is null)
        {
            seq = new TicketSequence { Date = today, LastSequence = 1 };
            _db.TicketSequences.Add(seq);
        }
        else
        {
            seq.LastSequence++;
        }

        await _db.SaveChangesAsync(ct);
        return $"HD-{today:yyyyMMdd}-{seq.LastSequence:D4}";
    }
}
