using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutBatches;

public record GetPayoutBatchesQuery(
    string? Status,
    WalletType? WalletType,
    int Page = 1,
    int PageSize = 20)
    : IRequest<Result<PagedResult<PayoutBatchRowDto>>>;
