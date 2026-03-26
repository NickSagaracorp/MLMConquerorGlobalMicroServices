using MediatR;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.ReverseSponsorBonus;

/// <summary>
/// Reverses a sponsor bonus when a member cancels within the 14-day chargeback window.
/// - Pending commission  → cancelled in place.
/// - Paid commission     → new negative-amount earning using CommissionType.ReverseId.
/// Idempotent — safe to call multiple times for the same order.
/// </summary>
public record ReverseSponsorBonusCommand(
    string CancelledMemberId,
    string SignupOrderId,
    string? Reason
) : IRequest<Result<CalculationResultResponse>>;
