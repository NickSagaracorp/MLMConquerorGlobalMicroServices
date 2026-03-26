using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetPresidentialBonusCommissions;

public record GetPresidentialBonusCommissionsQuery(int Page, int PageSize) : IRequest<Result<PagedResult<CommissionEarningDto>>>;
