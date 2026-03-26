using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetDualResidualCommissions;

public record GetDualResidualCommissionsQuery(int Page, int PageSize) : IRequest<Result<PagedResult<CommissionEarningDto>>>;
