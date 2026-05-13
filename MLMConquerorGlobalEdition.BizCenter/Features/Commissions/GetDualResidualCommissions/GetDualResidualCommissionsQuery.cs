using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetDualResidualCommissions;

public record GetDualResidualCommissionsQuery(
    int  Page,
    int  PageSize,
    int? Year  = null,
    int? Month = null) : IRequest<Result<PagedResult<CommissionEarningDto>>>;
