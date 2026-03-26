using MediatR;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.CalculatePresidentialBonus;

/// <summary>
/// Calculates the Presidential Bonus for ambassadors who hold the required lifetime rank.
/// Triggered monthly on the 1st at 4:00 AM UTC by HangFire.
/// </summary>
public record CalculatePresidentialBonusCommand(DateTime? PeriodDate = null)
    : IRequest<Result<CalculationResultResponse>>;
