using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissionsMonthBreakdown;

public record GetCommissionsMonthBreakdownQuery(int Year, int Month)
    : IRequest<Result<List<CommissionMonthBreakdownDto>>>;
