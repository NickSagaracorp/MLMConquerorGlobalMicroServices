using MediatR;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateSponsorBonus;

/// <summary>
/// Calculates the one-time sponsor bonus for the direct sponsor of a newly completed signup.
/// Idempotent — safe to call multiple times for the same order.
/// </summary>
public record CalculateSponsorBonusCommand(
    string NewMemberId,
    string OrderId
) : IRequest<Result<CalculationResultResponse>>;
