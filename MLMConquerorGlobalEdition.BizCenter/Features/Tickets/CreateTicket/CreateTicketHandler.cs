using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tickets.CreateTicket;

public class CreateTicketHandler : IRequestHandler<CreateTicketCommand, Result<TicketDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CreateTicketHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<TicketDto>> Handle(CreateTicketCommand command, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var req = command.Request;
        var now = _dateTime.UtcNow;

        // Validate category
        var category = await _db.TicketCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == req.CategoryId, ct);

        if (category is null)
            return Result<TicketDto>.Failure("CATEGORY_NOT_FOUND", "Ticket category not found.");

        var ticket = new Ticket
        {
            Id = Guid.NewGuid().ToString(),
            MemberId = memberId,
            Subject = req.Subject,
            Body = req.Body,
            CategoryId = req.CategoryId,
            Priority = req.Priority,
            Status = TicketStatus.Open,
            CreatedBy = _currentUser.UserId,
            CreationDate = now,
            LastUpdateDate = now,
            LastUpdateBy = _currentUser.UserId
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync(ct);

        var dto = new TicketDto
        {
            Id = ticket.Id,
            Subject = ticket.Subject,
            Body = ticket.Body,
            Status = ticket.Status.ToString(),
            Priority = ticket.Priority.ToString(),
            CategoryName = category.Name,
            CreationDate = ticket.CreationDate,
            AssignedToUserId = ticket.AssignedToUserId
        };

        return Result<TicketDto>.Success(dto);
    }
}
