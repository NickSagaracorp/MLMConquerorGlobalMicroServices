using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutStats;

public record GetPayoutStatsQuery(
    DateTime ProcessDate,
    int? CommissionTypeId = null
) : IRequest<Result<PayoutStatsDto>>;
