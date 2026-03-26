using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Billing;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Billing.GetBillingHistory;

public record GetBillingHistoryQuery(int Page, int PageSize) : IRequest<Result<PagedResult<OrderHistoryDto>>>;
