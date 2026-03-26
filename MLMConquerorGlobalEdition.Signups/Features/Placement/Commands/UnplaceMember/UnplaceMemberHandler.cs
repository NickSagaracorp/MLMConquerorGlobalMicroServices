using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.Signups.Services;

namespace MLMConquerorGlobalEdition.Signups.Features.Placement.Commands.UnplaceMember;

public class UnplaceMemberHandler : IRequestHandler<UnplaceMemberCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;

    public UnplaceMemberHandler(AppDbContext db, IDateTimeProvider dateTime)
    {
        _db = db;
        _dateTime = dateTime;
    }

    public async Task<Result<bool>> Handle(UnplaceMemberCommand command, CancellationToken ct)
    {
        var now = _dateTime.Now;

        var node = await _db.DualTeamTree.FirstOrDefaultAsync(x => x.MemberId == command.MemberId, ct);
        if (node is null)
            return Result<bool>.Failure("PLACEMENT_NOT_FOUND", $"Member '{command.MemberId}' has no active placement.");

        // Get placement log to validate unplacement rules
        var log = await _db.PlacementLogs
            .Where(x => x.MemberId == command.MemberId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

        if (log is not null)
        {
            // Rule: max 2 unplacements
            if (log.UnplacementCount >= 2)
                throw new UnplacementLimitExceededException();

            // Rule: within 72 hours of first placement
            if (log.FirstPlacementDate.HasValue && (now - log.FirstPlacementDate.Value).TotalHours > 72)
                throw new UnplacementWindowExpiredException();
        }

        _db.DualTeamTree.Remove(node);

        var unplacementLog = new PlacementLog
        {
            MemberId = command.MemberId,
            PlacedUnderMemberId = node.ParentMemberId ?? string.Empty,
            Side = node.Side,
            Action = "Unplaced",
            UnplacementCount = (log?.UnplacementCount ?? 0) + 1,
            FirstPlacementDate = log?.FirstPlacementDate,
            CreationDate = now,
            CreatedBy = command.RequestedByMemberId
        };

        await _db.PlacementLogs.AddAsync(unplacementLog, ct);
        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
