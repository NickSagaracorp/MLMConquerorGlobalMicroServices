using MediatR;
using MLMConquerorGlobalEdition.Billing.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Features.Payout;

public record PayoutCommand(PayoutRequest Request) : IRequest<Result<PayoutResponse>>;
