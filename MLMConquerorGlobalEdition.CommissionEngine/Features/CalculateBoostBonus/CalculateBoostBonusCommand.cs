using MediatR;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateBoostBonus;

/// <summary>
/// Calculates the Gold/Platinum Boost Bonus for all qualifying ambassadors.
/// Triggered weekly on Sunday at 3:00 AM UTC by HangFire.
/// PeriodDate defaults to the most recent Sunday (week start) if not provided.
/// </summary>
public record CalculateBoostBonusCommand(DateTime? PeriodDate = null)
    : IRequest<Result<CalculationResultResponse>>;
