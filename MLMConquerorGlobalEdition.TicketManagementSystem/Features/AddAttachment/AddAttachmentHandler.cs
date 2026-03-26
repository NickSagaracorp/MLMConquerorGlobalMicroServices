using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.AddAttachment;

public class AddAttachmentHandler : IRequestHandler<AddAttachmentCommand, Result<TicketAttachmentDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public AddAttachmentHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<TicketAttachmentDto>> Handle(AddAttachmentCommand request, CancellationToken ct)
    {
        var ticket = await _db.Tickets
            .Where(t => t.Id == request.TicketId && !t.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (ticket is null)
            return Result<TicketAttachmentDto>.Failure("TICKET_NOT_FOUND", "Ticket not found.");

        if (!_currentUser.IsAdmin && ticket.MemberId != _currentUser.MemberId)
            return Result<TicketAttachmentDto>.Failure("FORBIDDEN", "Access denied.");

        var now = _dateTime.Now;

        var attachment = new TicketAttachment
        {
            TicketId = ticket.Id,
            FileName = request.Request.FileName,
            FileUrl = request.Request.FileUrl,
            FileSizeBytes = request.Request.FileSizeBytes,
            ContentType = request.Request.ContentType,
            CreationDate = now,
            CreatedBy = _currentUser.UserId
        };

        await _db.TicketAttachments.AddAsync(attachment, ct);

        ticket.LastUpdateDate = now;
        ticket.LastUpdateBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        var dto = new TicketAttachmentDto
        {
            Id = attachment.Id,
            TicketId = attachment.TicketId,
            FileName = attachment.FileName,
            FileUrl = attachment.FileUrl,
            FileSizeBytes = attachment.FileSizeBytes,
            ContentType = attachment.ContentType,
            CreationDate = attachment.CreationDate
        };

        return Result<TicketAttachmentDto>.Success(dto);
    }
}
