using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporateEvents;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporateEvents.UpdateCorporateEvent;

public class UpdateCorporateEventHandler : IRequestHandler<UpdateCorporateEventCommand, Result<CorporateEventDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public UpdateCorporateEventHandler(AppDbContext db, ICurrentUserService cu, IDateTimeProvider dt)
    {
        _db = db;
        _currentUser = cu;
        _dateTime = dt;
    }

    public async Task<Result<CorporateEventDto>> Handle(
        UpdateCorporateEventCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.CorporateEvents
            .FirstOrDefaultAsync(e => e.Id == request.EventId, cancellationToken);

        if (entity is null)
            return Result<CorporateEventDto>.Failure("EVENT_NOT_FOUND", "Event not found.");

        var req = request.Request;
        entity.Title = req.Title;
        entity.Description = req.Description;
        entity.EventDate = req.EventDate;
        entity.Location = req.Location;
        entity.ImageUrl = req.ImageUrl;
        entity.IsActive = req.IsActive;
        entity.LastUpdateDate = _dateTime.Now;
        entity.LastUpdateBy = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);

        return Result<CorporateEventDto>.Success(new CorporateEventDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Description = entity.Description,
            EventDate = entity.EventDate,
            Location = entity.Location,
            ImageUrl = entity.ImageUrl,
            IsActive = entity.IsActive,
            CreationDate = entity.CreationDate
        });
    }
}
