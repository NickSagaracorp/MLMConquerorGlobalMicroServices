using MediatR;
using MLMConquerorGlobalEdition.Billing.DTOs;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Features.GetGateways;

public class GetGatewaysHandler : IRequestHandler<GetGatewaysQuery, Result<IEnumerable<GatewayInfoDto>>>
{
    private readonly IGatewayResolver _gatewayResolver;

    public GetGatewaysHandler(IGatewayResolver gatewayResolver)
        => _gatewayResolver = gatewayResolver;

    public Task<Result<IEnumerable<GatewayInfoDto>>> Handle(GetGatewaysQuery query, CancellationToken ct)
    {
        var gateways = _gatewayResolver.AvailableGateways
            .Select(type => new GatewayInfoDto
            {
                Id = (int)type,
                Name = type.ToString(),
                IsAvailable = true
            })
            .ToList();

        return Task.FromResult(Result<IEnumerable<GatewayInfoDto>>.Success(gateways));
    }
}
