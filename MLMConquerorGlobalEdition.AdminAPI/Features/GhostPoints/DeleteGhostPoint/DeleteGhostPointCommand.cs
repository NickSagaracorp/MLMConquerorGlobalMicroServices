using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.GhostPoints.DeleteGhostPoint;

public record DeleteGhostPointCommand(string GhostPointId) : IRequest<Result<bool>>;
