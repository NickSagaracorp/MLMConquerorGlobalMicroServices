using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetFastStartBonusCommissions;

public record GetFastStartBonusCommissionsQuery(int Page, int PageSize) : IRequest<Result<PagedResult<CommissionEarningDto>>>;
