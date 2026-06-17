using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.VerifyPayoutReceipt;

public record VerifyPayoutReceiptCommand(long AttemptId) : IRequest<Result<ReceiptVerificationDto>>;
