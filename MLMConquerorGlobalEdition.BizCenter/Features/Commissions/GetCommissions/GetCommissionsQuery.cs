using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissions;

public record GetCommissionsQuery(int Page, int PageSize) : IRequest<Result<PagedResult<CommissionEarningDto>>>;
