using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Signups.Features.Placement.Queries.ValidatePlacement;

/// <summary>
/// Checks whether a binary tree position is available before the caller commits to placing.
/// Returns Success(true) if the position is open, or Failure with a reason if not.
/// </summary>
public record ValidatePlacementQuery(
    string MemberId,
    string PlaceUnderMemberId,
    string Side) : IRequest<Result<bool>>;
