using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Dashboard;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Dashboard.GetDashboardStats;

public record GetDashboardStatsQuery : IRequest<Result<DashboardStatsDto>>;
