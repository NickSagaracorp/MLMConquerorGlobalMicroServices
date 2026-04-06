using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Placement;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Placement.GetPendingPlacements;

/// <summary>
/// Returns all enrolled members sponsored by the current ambassador
/// that either have no placement yet or are within the 72-hour correction window.
/// </summary>
public record GetPendingPlacementsQuery : IRequest<Result<IEnumerable<PendingPlacementDto>>>;
