using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.RecurringPerformance;
using MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.RecurringPerformance.PreviewPlan;

/// <summary>
/// Delegates to IRecurringBillingPlanner.PreviewAsync (dry-run mode —
/// no RecurringBillingBatch / RecurringBillingBatchShard rows are written)
/// and maps the result to the API contract DTO.
/// </summary>
public class PreviewRecurringPerformanceHandler
    : IRequestHandler<PreviewRecurringPerformanceQuery, Result<RecurringPerformancePreviewDto>>
{
    private readonly IRecurringBillingPlanner _planner;
    private readonly IDateTimeProvider        _dateTime;

    public PreviewRecurringPerformanceHandler(
        IRecurringBillingPlanner planner,
        IDateTimeProvider dateTime)
    {
        _planner  = planner;
        _dateTime = dateTime;
    }

    public async Task<Result<RecurringPerformancePreviewDto>> Handle(
        PreviewRecurringPerformanceQuery request, CancellationToken cancellationToken)
    {
        var runDate = request.RunDate ?? _dateTime.Now.Date;

        var preview = await _planner.PreviewAsync(runDate, cancellationToken);
        if (!preview.IsSuccess)
            return Result<RecurringPerformancePreviewDto>.Failure(preview.ErrorCode!, preview.Error!);

        var p = preview.Value!;

        var perGateway = p.PerGateway
            .Select(r => new GatewayPreviewRowDto
            {
                Processor       = r.Processor,
                CasesAssigned   = r.CasesAssigned,
                AvgLatencyMs    = r.AvgLatencyMs,
                WorkersNeeded   = r.WorkersNeeded,
                HitFloor        = r.HitFloor,
                HitCeiling      = r.HitCeiling
            })
            .ToList();

        var dto = new RecurringPerformancePreviewDto
        {
            AsOfUtc           = p.AsOfUtc,
            PendingBills      = p.PendingBills,
            TargetWindowHours = p.TargetWindowHours,
            PerGateway        = perGateway,
            Estimated         = new EstimatedCompletionDto
            {
                CompletionMinutes = p.EstimatedCompletionMinutes,
                WithinTarget      = p.WithinTarget
            },
            Notes             = p.Notes
        };

        return Result<RecurringPerformancePreviewDto>.Success(dto);
    }
}
