using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;
using MLMConquerorGlobalEdition.SignupAPI.Services;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Placement.Commands.PlaceMember;

public class PlaceMemberHandler : IRequestHandler<PlaceMemberCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly IPushNotificationService _push;

    public PlaceMemberHandler(AppDbContext db, IDateTimeProvider dateTime, IPushNotificationService push)
    {
        _db = db;
        _dateTime = dateTime;
        _push = push;
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

        // Notify the placed member
        _ = _push.SendAsync(
            command.MemberId,
            NotificationEvents.PlacementCompleted,
            "Placement Completed",
            $"You have been placed in the {command.Side} position of your team.",
            ct);

        // Notify all uplines in the dual team hierarchy
        var uplineIds = parentPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(id => id != command.MemberId)
            .ToList();

        foreach (var uplineId in uplineIds)
        {
            _ = _push.SendAsync(
                uplineId,
                NotificationEvents.DownlinePlaced,
                "New Team Placement",
                $"A new member has been placed in your downline ({command.Side} leg).",
                ct);
        }

        return Result<bool>.Success(true);
    }
}
