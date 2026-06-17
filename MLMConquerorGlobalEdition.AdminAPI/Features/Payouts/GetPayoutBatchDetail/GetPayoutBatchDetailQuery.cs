using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutBatchDetail;

public record GetPayoutBatchDetailQuery(string BatchId)
    : IRequest<Result<PayoutBatchDetailDto>>;
