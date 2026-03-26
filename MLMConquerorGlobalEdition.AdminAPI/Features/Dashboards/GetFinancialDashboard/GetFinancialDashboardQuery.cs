using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Dashboards;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetFinancialDashboard;

public record GetFinancialDashboardQuery : IRequest<Result<FinancialDashboardDto>>;
