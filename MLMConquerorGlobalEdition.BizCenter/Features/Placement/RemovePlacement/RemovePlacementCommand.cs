using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Placement.RemovePlacement;

/// <summary>
/// Removes a member's placement from the Dual Team tree.
/// Ambassador: only within 72h of first placement and with opportunities remaining.
/// Admin: no time restriction.
/// </summary>
public record RemovePlacementCommand(string MemberToRemoveId)
    : IRequest<Result<RemovePlacementResult>>;

public record RemovePlacementResult(
    string MemberId,
    string FullName,
    int    OpportunitiesRemaining
);
