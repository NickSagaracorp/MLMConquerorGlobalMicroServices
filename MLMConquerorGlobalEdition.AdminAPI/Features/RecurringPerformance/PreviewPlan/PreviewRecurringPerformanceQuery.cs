using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.RecurringPerformance;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.RecurringPerformance.PreviewPlan;

/// <summary>
/// Triggers the planner's dry-run (no rows written).
/// RunDate defaults to today (UTC) if not supplied.
/// </summary>
public record PreviewRecurringPerformanceQuery(
    DateTime? RunDate = null
) : IRequest<Result<RecurringPerformancePreviewDto>>;
