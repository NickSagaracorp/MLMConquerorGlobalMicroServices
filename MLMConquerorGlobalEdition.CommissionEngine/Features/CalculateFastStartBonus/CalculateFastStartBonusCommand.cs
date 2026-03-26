using MediatR;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateFastStartBonus;

/// <summary>
/// Triggered in real-time when a new member order is completed.
/// Walks the sponsor chain and creates CommissionEarning records for each
/// upline member whose Fast Start Bonus window is currently active.
/// </summary>
public record CalculateFastStartBonusCommand(
    string OrderId,
    string BuyerMemberId
) : IRequest<Result<CalculationResultResponse>>;
