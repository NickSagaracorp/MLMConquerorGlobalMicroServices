using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetBoostBonusCommissions;

public record GetBoostBonusCommissionsQuery(int Page, int PageSize) : IRequest<Result<PagedResult<CommissionEarningDto>>>;
