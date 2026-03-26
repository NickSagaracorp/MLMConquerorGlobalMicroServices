using MediatR;
using MLMConquerorGlobalEdition.Billing.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Features.Charge;

public record ChargeCommand(ChargeRequest Request) : IRequest<Result<ChargeResponse>>;
