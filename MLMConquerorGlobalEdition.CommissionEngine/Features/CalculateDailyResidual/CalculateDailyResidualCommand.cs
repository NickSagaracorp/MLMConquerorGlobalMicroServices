using MediatR;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateDailyResidual;

/// <summary>
/// Calculates the binary (Dual Team) daily residual for all active ambassadors.
/// Triggered nightly at 2:00 AM UTC by HangFire.
/// PeriodDate defaults to today if not provided.
/// </summary>
public record CalculateDailyResidualCommand(DateTime? PeriodDate = null)
    : IRequest<Result<CalculationResultResponse>>;
