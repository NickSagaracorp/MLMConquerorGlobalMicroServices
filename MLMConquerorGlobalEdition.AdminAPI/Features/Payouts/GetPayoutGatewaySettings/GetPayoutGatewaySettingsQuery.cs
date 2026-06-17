using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutGatewaySettings;

public record GetPayoutGatewaySettingsQuery() : IRequest<Result<List<PayoutGatewayDto>>>;
