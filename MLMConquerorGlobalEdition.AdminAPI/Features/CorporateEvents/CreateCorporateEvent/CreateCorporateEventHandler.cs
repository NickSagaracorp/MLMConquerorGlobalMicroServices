using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporateEvents;
using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporateEvents.CreateCorporateEvent;

public class CreateCorporateEventHandler : IRequestHandler<CreateCorporateEventCommand, Result<CorporateEventDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CreateCorporateEventHandler(AppDbContext db, ICurrentUserService cu, IDateTimeProvider dt)
    {
        _db = db;
        _currentUser = cu;
        _dateTime = dt;
    }

    public async Task<Result<CorporateEventDto>> Handle(
        CreateCorporateEventCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        var now = _dateTime.Now;

        var entity = new CorporateEvent
        {
            Title = req.Title,
            Description = req.Description,
            EventDate = req.EventDate,
            Location = req.Location,
            ImageUrl = req.ImageUrl,
            IsActive = true,
            CreationDate = now,
            CreatedBy = _currentUser.UserId,
            LastUpdateDate = now,
            LastUpdateBy = _currentUser.UserId
        };

        await _db.CorporateEvents.AddAsync(entity, cancellationToken);
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
