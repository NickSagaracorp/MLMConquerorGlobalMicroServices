using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.LoyaltyAdmin.UnlockLoyaltyPoints;

public class UnlockLoyaltyPointsHandler : IRequestHandler<UnlockLoyaltyPointsCommand, Result<int>>
{
    private readonly AppDbContext       _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider  _clock;

    public UnlockLoyaltyPointsHandler(
        AppDbContext        db,
        ICurrentUserService currentUser,
        IDateTimeProvider   clock)
    {
        _db          = db;
        _currentUser = currentUser;
        _clock       = clock;
    }

    public async Task<Result<int>> Handle(UnlockLoyaltyPointsCommand command, CancellationToken ct)
    {
        var memberExists = await _db.MemberProfiles
            .AnyAsync(m => m.MemberId == command.MemberId && !m.IsDeleted, ct);

        if (!memberExists)
            return Result<int>.Failure("MEMBER_NOT_FOUND", $"Member '{command.MemberId}' not found.");

        var now = _clock.Now;

        if (!string.IsNullOrWhiteSpace(command.LoyaltyPointsId))
        {
            // Single-record unlock
            var record = await _db.LoyaltyPoints
                .FirstOrDefaultAsync(lp => lp.Id == command.LoyaltyPointsId
                                        && lp.MemberId == command.MemberId, ct);

            if (record is null)
                return Result<int>.Failure("RECORD_NOT_FOUND",
                    $"Loyalty record '{command.LoyaltyPointsId}' not found for member '{command.MemberId}'.");

            if (!record.IsLocked)
                return Result<int>.Failure("ALREADY_UNLOCKED", "This loyalty record is already unlocked.");

            record.IsLocked      = false;
            record.UnlockedAt    = now;
            record.LastUpdateDate = now;
            record.LastUpdateBy  = _currentUser.UserId;

            await _db.SaveChangesAsync(ct);
            return Result<int>.Success(1);
        }

        // Bulk unlock — all locked records for the member
        var locked = await _db.LoyaltyPoints
            .Where(lp => lp.MemberId == command.MemberId && lp.IsLocked)
            .ToListAsync(ct);

        if (locked.Count == 0)
            return Result<int>.Failure("NOTHING_TO_UNLOCK",
                "No locked loyalty records found for this member.");

        foreach (var lp in locked)
        {
            lp.IsLocked      = false;
            lp.UnlockedAt    = now;
            lp.LastUpdateDate = now;
            lp.LastUpdateBy  = _currentUser.UserId;
        }

        await _db.SaveChangesAsync(ct);
        return Result<int>.Success(locked.Count);
    }
}
