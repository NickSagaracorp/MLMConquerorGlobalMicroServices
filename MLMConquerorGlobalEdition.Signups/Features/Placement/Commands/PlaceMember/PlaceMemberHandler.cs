using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.Signups.Services;

namespace MLMConquerorGlobalEdition.Signups.Features.Placement.Commands.PlaceMember;

public class PlaceMemberHandler : IRequestHandler<PlaceMemberCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;

    public PlaceMemberHandler(AppDbContext db, IDateTimeProvider dateTime)
    {
        _db = db;
        _dateTime = dateTime;
    }

    public async Task<Result<bool>> Handle(PlaceMemberCommand command, CancellationToken ct)
    {
        var now = _dateTime.Now;

        var member = await _db.MemberProfiles.FirstOrDefaultAsync(x => x.MemberId == command.MemberId, ct);
        if (member is null)
            return Result<bool>.Failure("MEMBER_NOT_FOUND", $"Member '{command.MemberId}' not found.");

        // Validate 30-day placement window
        if ((now - member.EnrollDate).TotalDays > 30)
            throw new PlacementWindowExpiredException();

        var parent = await _db.MemberProfiles.FirstOrDefaultAsync(x => x.MemberId == command.PlaceUnderMemberId, ct);
        if (parent is null)
            return Result<bool>.Failure("PARENT_MEMBER_NOT_FOUND", $"Member '{command.PlaceUnderMemberId}' not found.");

        var side = Enum.Parse<TreeSide>(command.Side);

        // Check if position is already occupied
        var positionOccupied = await _db.DualTeamTree.AnyAsync(
            x => x.ParentMemberId == command.PlaceUnderMemberId && x.Side == side, ct);
        if (positionOccupied)
            return Result<bool>.Failure("POSITION_OCCUPIED", $"The {command.Side} position under '{command.PlaceUnderMemberId}' is already occupied.");

        // Get parent's hierarchy path
        var parentNode = await _db.DualTeamTree.FirstOrDefaultAsync(x => x.MemberId == command.PlaceUnderMemberId, ct);
        var parentPath = parentNode?.HierarchyPath ?? $"/{command.PlaceUnderMemberId}";

        var node = new DualTeamEntity
        {
            MemberId = command.MemberId,
            ParentMemberId = command.PlaceUnderMemberId,
            Side = side,
            HierarchyPath = $"{parentPath}/{command.MemberId}",
            CreatedBy = command.MemberId,
            CreationDate = now,
            LastUpdateDate = now
        };

        var log = new PlacementLog
        {
            MemberId = command.MemberId,
            PlacedUnderMemberId = command.PlaceUnderMemberId,
            Side = side,
            Action = "Placed",
            FirstPlacementDate = now,
            CreationDate = now,
            CreatedBy = command.MemberId
        };

        await _db.DualTeamTree.AddAsync(node, ct);
        await _db.PlacementLogs.AddAsync(log, ct);
        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
