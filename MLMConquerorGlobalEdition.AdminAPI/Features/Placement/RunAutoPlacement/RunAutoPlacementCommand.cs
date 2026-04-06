using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Placement.RunAutoPlacement;

public record RunAutoPlacementCommand : IRequest<Result<RunAutoPlacementResult>>;

public record RunAutoPlacementResult(int PlacedCount, IEnumerable<string> PlacedMemberIds);
