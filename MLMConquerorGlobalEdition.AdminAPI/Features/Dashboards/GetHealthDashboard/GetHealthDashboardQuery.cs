using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Dashboards;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetHealthDashboard;

public record GetHealthDashboardQuery : IRequest<Result<HealthDashboardDto>>;
