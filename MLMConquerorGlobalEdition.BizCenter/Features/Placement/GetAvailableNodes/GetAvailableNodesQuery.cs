using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Placement;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Placement.GetAvailableNodes;

/// <summary>
/// Returns the sponsor's Dual Team subtree showing which nodes have available slots.
/// Used by the Placement Wizard node selector.
/// </summary>
public record GetAvailableNodesQuery(string MemberToPlaceId)
    : IRequest<Result<AvailableNodesResponse>>;
