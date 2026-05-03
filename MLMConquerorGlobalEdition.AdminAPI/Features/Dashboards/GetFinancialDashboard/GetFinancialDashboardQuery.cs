using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Dashboards;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetFinancialDashboard;

public record GetFinancialDashboardQuery(bool BypassCache = false) : IRequest<Result<FinancialDashboardDto>>;
