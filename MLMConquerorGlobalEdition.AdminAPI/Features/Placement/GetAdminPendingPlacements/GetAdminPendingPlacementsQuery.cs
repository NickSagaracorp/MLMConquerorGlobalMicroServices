using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Placement;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Placement.GetAdminPendingPlacements;

public record GetAdminPendingPlacementsQuery(string? SponsorId = null)
    : IRequest<Result<IEnumerable<AdminPendingPlacementDto>>>;
