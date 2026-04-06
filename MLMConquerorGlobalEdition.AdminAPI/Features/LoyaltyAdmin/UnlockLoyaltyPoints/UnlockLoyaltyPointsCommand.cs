using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.LoyaltyAdmin.UnlockLoyaltyPoints;

/// <summary>
/// Unlocks loyalty points for a member.
/// If <see cref="LoyaltyPointsId"/> is provided, only that record is unlocked.
/// Otherwise, all locked records for the member are unlocked in bulk.
/// </summary>
public record UnlockLoyaltyPointsCommand(
    string MemberId,
    string? LoyaltyPointsId = null
) : IRequest<Result<int>>;
