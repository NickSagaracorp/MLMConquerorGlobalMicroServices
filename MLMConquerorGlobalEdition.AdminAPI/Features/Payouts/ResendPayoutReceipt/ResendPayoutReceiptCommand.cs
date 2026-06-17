using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.ResendPayoutReceipt;

public record ResendPayoutReceiptCommand(long AttemptId) : IRequest<Result<bool>>;
