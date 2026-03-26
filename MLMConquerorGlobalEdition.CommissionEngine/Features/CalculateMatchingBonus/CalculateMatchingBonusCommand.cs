using MediatR;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateMatchingBonus;

/// <summary>
/// Calculates the Matching Bonus: a percentage of what each direct downline member
/// earned in commissions during the given period.
/// Runs after Daily Residual has been calculated.
/// </summary>
public record CalculateMatchingBonusCommand(DateTime? PeriodDate = null)
    : IRequest<Result<CalculationResultResponse>>;
