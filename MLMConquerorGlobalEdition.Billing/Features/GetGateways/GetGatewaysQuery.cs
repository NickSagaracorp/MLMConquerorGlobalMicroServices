using MediatR;
using MLMConquerorGlobalEdition.Billing.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Features.GetGateways;

public record GetGatewaysQuery : IRequest<Result<IEnumerable<GatewayInfoDto>>>;
