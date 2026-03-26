using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporateEvents.DeleteCorporateEvent;

public class DeleteCorporateEventHandler : IRequestHandler<DeleteCorporateEventCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _cu;
    private readonly IDateTimeProvider _dt;

    public DeleteCorporateEventHandler(AppDbContext db, ICurrentUserService cu, IDateTimeProvider dt)
    {
        _db = db;
        _cu = cu;
        _dt = dt;
    }

    public async Task<Result<bool>> Handle(
        DeleteCorporateEventCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.CorporateEvents
            .FirstOrDefaultAsync(e => e.Id == request.EventId, cancellationToken);

        if (entity is null)
            return Result<bool>.Failure("EVENT_NOT_FOUND", "Event not found.");

        entity.IsActive = false;
        entity.LastUpdateDate = _dt.Now;
        entity.LastUpdateBy = _cu.UserId;

        await _db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
