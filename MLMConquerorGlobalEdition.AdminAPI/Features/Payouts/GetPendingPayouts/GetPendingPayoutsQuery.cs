using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPendingPayouts;

public record GetPendingPayoutsQuery(
    DateTime ProcessDate,
    int Page = 1,
    int PageSize = 20,
    WalletType? WalletType = null,
    int? CommissionTypeId = null
) : IRequest<Result<PagedResult<PendingPayoutRowDto>>>;
