using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutGatewayLog;

public record GetPayoutGatewayLogQuery(string MemberId, long AttemptId) : IRequest<Result<List<PayoutGatewayLogDto>>>;
