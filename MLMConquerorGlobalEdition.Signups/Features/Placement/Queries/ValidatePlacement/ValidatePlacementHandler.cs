using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.Signups.Services;

namespace MLMConquerorGlobalEdition.Signups.Features.Placement.Queries.ValidatePlacement;

public class ValidatePlacementHandler : IRequestHandler<ValidatePlacementQuery, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;

    public ValidatePlacementHandler(AppDbContext db, IDateTimeProvider dateTime)
    {
        _db = db;
        _dateTime = dateTime;
    }

    public async Task<Result<bool>> Handle(ValidatePlacementQuery query, CancellationToken ct)
    {
        var now = _dateTime.Now;

        // Member to be placed must exist
        var member = await _db.MemberProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MemberId == query.MemberId, ct);

        if (member is null)
            return Result<bool>.Failure("MEMBER_NOT_FOUND", $"Member '{query.MemberId}' not found.");

        // Placement window: 30 days from enrollment
        if ((now - member.EnrollDate).TotalDays > 30)
            return Result<bool>.Failure(
                "PLACEMENT_WINDOW_EXPIRED",
                "The 30-day placement window for this member has expired.");

        // Target parent must exist
        var parentExists = await _db.MemberProfiles
            .AsNoTracking()
            .AnyAsync(x => x.MemberId == query.PlaceUnderMemberId, ct);

        if (!parentExists)
            return Result<bool>.Failure(
                "PARENT_MEMBER_NOT_FOUND", $"Member '{query.PlaceUnderMemberId}' not found.");

        if (!Enum.TryParse<TreeSide>(query.Side, ignoreCase: true, out var side))
            return Result<bool>.Failure("INVALID_SIDE", "Side must be 'Left' or 'Right'.");

        // Check if the position is already occupied
        var positionOccupied = await _db.DualTeamTree
            .AsNoTracking()
            .AnyAsync(x => x.ParentMemberId == query.PlaceUnderMemberId && x.Side == side, ct);

        if (positionOccupied)
            return Result<bool>.Failure(
                "POSITION_OCCUPIED",
                $"The {query.Side} position under '{query.PlaceUnderMemberId}' is already occupied.");

        return Result<bool>.Success(true);
    }
}
