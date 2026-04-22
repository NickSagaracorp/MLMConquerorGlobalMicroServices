using MediatR;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateCarBonus;

/// <summary>
/// Calculates Car Bonus for all qualifying ambassadors for a given calendar month.
/// PeriodDate defaults to the first day of the current month when not provided.
/// Triggered monthly on the 1st at 5:00 AM UTC by HangFire.
/// </summary>
public record CalculateCarBonusCommand(DateTime? PeriodDate = null)
    : IRequest<Result<CalculationResultResponse>>;
