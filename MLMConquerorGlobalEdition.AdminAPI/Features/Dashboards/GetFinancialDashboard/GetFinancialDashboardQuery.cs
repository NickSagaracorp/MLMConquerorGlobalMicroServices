using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Dashboards;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetFinancialDashboard;

public record GetFinancialDashboardQuery(
    DateTime? From = null,
    DateTime? To   = null,
    bool BypassCache = false) : IRequest<Result<FinancialDashboardDto>>;
