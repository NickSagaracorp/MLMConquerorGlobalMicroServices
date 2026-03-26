using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.GhostPoints;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.GhostPoints.CreateGhostPoint;

public record CreateGhostPointCommand(CreateGhostPointRequest Request) : IRequest<Result<GhostPointDto>>;
