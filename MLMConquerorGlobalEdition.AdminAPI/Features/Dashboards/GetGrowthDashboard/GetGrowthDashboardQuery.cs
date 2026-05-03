using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Dashboards;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetGrowthDashboard;

public record GetGrowthDashboardQuery(bool BypassCache = false) : IRequest<Result<GrowthDashboardDto>>;
