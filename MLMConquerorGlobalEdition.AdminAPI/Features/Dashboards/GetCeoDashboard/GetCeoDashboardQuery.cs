using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Dashboards;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetCeoDashboard;

public record GetCeoDashboardQuery : IRequest<Result<CeoDashboardDto>>;
