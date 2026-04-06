using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Placement.AdminRemovePlacement;

public record AdminRemovePlacementCommand(string MemberId) : IRequest<Result<string>>;
