using MediatR;
using MLMConquerorGlobalEdition.Billing.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Features.Refund;

public record RefundCommand(RefundRequest Request) : IRequest<Result<bool>>;
