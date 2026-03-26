using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.GhostPoints;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.GhostPoints.CreateGhostPoint;

public class CreateGhostPointHandler : IRequestHandler<CreateGhostPointCommand, Result<GhostPointDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CreateGhostPointHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<GhostPointDto>> Handle(
        CreateGhostPointCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        var now = _dateTime.Now;

        var entity = new GhostPointEntity
        {
            MemberId = req.MemberId,
            LegMemberId = req.LegMemberId,
            Points = req.Points,
            Side = req.Side,
            AdminNote = req.Notes,
            IsActive = true,
            CreationDate = now,
            CreatedBy = _currentUser.UserId,
            LastUpdateDate = now,
            LastUpdateBy = _currentUser.UserId
        };

        await _db.GhostPoints.AddAsync(entity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = new GhostPointDto
        {
            Id = entity.Id,
            MemberId = entity.MemberId,
            LegMemberId = entity.LegMemberId,
            Points = entity.Points,
            Side = entity.Side.ToString(),
            Notes = entity.AdminNote,
            IsActive = entity.IsActive,
            CreationDate = entity.CreationDate
        };

        return Result<GhostPointDto>.Success(dto);
    }
}
