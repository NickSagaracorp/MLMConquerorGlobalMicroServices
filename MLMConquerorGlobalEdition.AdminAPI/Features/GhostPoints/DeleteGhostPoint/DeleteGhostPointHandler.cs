using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.GhostPoints.DeleteGhostPoint;

public class DeleteGhostPointHandler : IRequestHandler<DeleteGhostPointCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public DeleteGhostPointHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<bool>> Handle(
        DeleteGhostPointCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.GhostPoints
            .FirstOrDefaultAsync(g => g.Id == request.GhostPointId, cancellationToken);

        if (entity is null)
            return Result<bool>.Failure("GHOST_POINT_NOT_FOUND", $"Ghost point '{request.GhostPointId}' not found.");

        entity.IsActive = false;
        entity.LastUpdateDate = _dateTime.Now;
        entity.LastUpdateBy = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
