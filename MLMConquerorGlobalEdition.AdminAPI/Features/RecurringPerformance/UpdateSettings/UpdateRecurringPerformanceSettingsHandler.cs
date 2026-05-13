using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.RecurringPerformance;
using MLMConquerorGlobalEdition.AdminAPI.Features.RecurringPerformance.GetSettings;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.RecurringPerformance.UpdateSettings;

/// <summary>
/// Validates and persists all §10.7 high-volume tunables in a single transaction.
/// Validation rules mirror the controller contract:
/// - targetCompletionWindowHours ∈ [1,24]
/// - batchStartTimeUtc matches HH:mm (24h)
/// - per gateway: minWorkers ≥ 1, maxConcurrency ≥ minWorkers, windowOffsetMinutes ∈ [0,720]
/// - latencySamplingDays ∈ [1,90]
/// - cascadeStrategy ∈ SupportedCascadeStrategies
/// - aggregatorTriggerMode ∈ SupportedAggregatorTriggerModes
/// - all 7 CardProcessor rows must be present (no more, no less)
/// </summary>
public class UpdateRecurringPerformanceSettingsHandler
    : IRequestHandler<UpdateRecurringPerformanceSettingsCommand, Result<RecurringPerformanceSettingsDto>>
{
    private readonly AppDbContext    _db;
    private readonly IDateTimeProvider _dateTime;

    private static readonly List<string> SupportedCascadeStrategies      = new() { "DeferredUplineRollup" };
    private static readonly List<string> SupportedAggregatorTriggerModes = new() { "AfterAllChargeWorkers" };
    private static readonly Regex        BatchTimeRegex                   = new(@"^([01]\d|2[0-3]):[0-5]\d$");

    public UpdateRecurringPerformanceSettingsHandler(AppDbContext db, IDateTimeProvider dateTime)
    {
        _db       = db;
        _dateTime = dateTime;
    }

    public async Task<Result<RecurringPerformanceSettingsDto>> Handle(
        UpdateRecurringPerformanceSettingsCommand command, CancellationToken cancellationToken)
    {
        var req = command.Request;

        // ── Validate window settings ─────────────────────────────────────────────
        if (req.Window.TargetCompletionWindowHours < 1 || req.Window.TargetCompletionWindowHours > 24)
            return Result<RecurringPerformanceSettingsDto>.Failure("VALIDATION",
                "targetCompletionWindowHours must be between 1 and 24.");

        if (!BatchTimeRegex.IsMatch(req.Window.BatchStartTimeUtc ?? string.Empty))
            return Result<RecurringPerformanceSettingsDto>.Failure("VALIDATION",
                "batchStartTimeUtc must match HH:mm in 24-hour format (e.g. 05:00).");

        // ── Validate latencySamplingDays ─────────────────────────────────────────
        if (req.LatencySamplingDays < 1 || req.LatencySamplingDays > 90)
            return Result<RecurringPerformanceSettingsDto>.Failure("VALIDATION",
                "latencySamplingDays must be between 1 and 90.");

        // ── Validate cascadeStrategy / aggregatorTriggerMode ─────────────────────
        if (!SupportedCascadeStrategies.Contains(req.CascadeStrategy ?? string.Empty))
            return Result<RecurringPerformanceSettingsDto>.Failure("VALIDATION",
                $"cascadeStrategy '{req.CascadeStrategy}' is not supported. Supported: {string.Join(", ", SupportedCascadeStrategies)}.");

        if (!SupportedAggregatorTriggerModes.Contains(req.AggregatorTriggerMode ?? string.Empty))
            return Result<RecurringPerformanceSettingsDto>.Failure("VALIDATION",
                $"aggregatorTriggerMode '{req.AggregatorTriggerMode}' is not supported. Supported: {string.Join(", ", SupportedAggregatorTriggerModes)}.");

        // ── Validate perGateway rows ─────────────────────────────────────────────
        var validationError = ValidatePerGatewayRows(req.PerGateway);
        if (validationError is not null)
            return Result<RecurringPerformanceSettingsDto>.Failure("VALIDATION", validationError);

        // ── Persist in single transaction ────────────────────────────────────────
        var now = _dateTime.Now;

        var existingParams = await _db.GlobalParameters.ToListAsync(cancellationToken);

        UpsertParameter(existingParams, "RecurringBilling:TargetCompletionWindowHours",
            req.Window.TargetCompletionWindowHours.ToString(), now);
        UpsertParameter(existingParams, "RecurringBilling:BatchStartTimeUtc",
            req.Window.BatchStartTimeUtc ?? string.Empty, now);
        UpsertParameter(existingParams, "RecurringBilling:LatencySamplingDays",
            req.LatencySamplingDays.ToString(), now);
        UpsertParameter(existingParams, "RecurringBilling:CascadeStrategy",
            req.CascadeStrategy ?? string.Empty, now);
        UpsertParameter(existingParams, "RecurringBilling:AggregatorTriggerMode",
            req.AggregatorTriggerMode ?? string.Empty, now);

        foreach (var row in req.PerGateway)
        {
            UpsertParameter(existingParams, $"RecurringBilling:MinWorkersPerGateway:{row.Processor}",
                row.MinWorkers.ToString(), now);
            UpsertParameter(existingParams, $"RecurringBilling:MaxConcurrencyPerGateway:{row.Processor}",
                row.MaxConcurrency.ToString(), now);
            UpsertParameter(existingParams, $"RecurringBilling:GatewayWindowOffsetMinutes:{row.Processor}",
                row.WindowOffsetMinutes.ToString(), now);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // ── Return the saved state as confirmation ────────────────────────────────
        var responseWindow = new WindowSettingsDto
        {
            TargetCompletionWindowHours = req.Window.TargetCompletionWindowHours,
            BatchStartTimeUtc           = req.Window.BatchStartTimeUtc ?? string.Empty
        };

        var responsePerGateway = GetRecurringPerformanceSettingsHandler.ProcessorOrder
            .Select(processor =>
            {
                var name = processor.ToString();
                var row  = req.PerGateway.FirstOrDefault(r => r.Processor == name);
                return new GatewayPerformanceRowDto
                {
                    Processor           = name,
                    MinWorkers          = row?.MinWorkers ?? 2,
                    MaxConcurrency      = row?.MaxConcurrency ?? 10,
                    WindowOffsetMinutes = row?.WindowOffsetMinutes ?? 0
                };
            })
            .ToList();

        var dto = new RecurringPerformanceSettingsDto
        {
            Window                          = responseWindow,
            PerGateway                      = responsePerGateway,
            LatencySamplingDays             = req.LatencySamplingDays,
            CascadeStrategy                 = req.CascadeStrategy ?? string.Empty,
            AggregatorTriggerMode           = req.AggregatorTriggerMode ?? string.Empty,
            SupportedCascadeStrategies      = SupportedCascadeStrategies,
            SupportedAggregatorTriggerModes = SupportedAggregatorTriggerModes
        };

        return Result<RecurringPerformanceSettingsDto>.Success(dto);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static string? ValidatePerGatewayRows(List<GatewayPerformanceRowDto>? rows)
    {
        if (rows is null || rows.Count == 0)
            return "perGateway must contain exactly 7 processor rows.";

        var expectedNames = GetRecurringPerformanceSettingsHandler.ProcessorOrder
            .Select(p => p.ToString())
            .ToHashSet();

        var receivedNames = rows.Select(r => r.Processor).ToHashSet();

        var missing = expectedNames.Except(receivedNames).ToList();
        if (missing.Count > 0)
            return $"perGateway is missing rows for: {string.Join(", ", missing)}.";

        var extra = receivedNames.Except(expectedNames).ToList();
        if (extra.Count > 0)
            return $"perGateway contains unknown processor(s): {string.Join(", ", extra)}.";

        foreach (var row in rows)
        {
            if (row.MinWorkers < 1)
                return $"perGateway[{row.Processor}].minWorkers must be >= 1.";

            if (row.MaxConcurrency < row.MinWorkers)
                return $"perGateway[{row.Processor}].maxConcurrency must be >= minWorkers ({row.MinWorkers}).";

            if (row.WindowOffsetMinutes < 0 || row.WindowOffsetMinutes > 720)
                return $"perGateway[{row.Processor}].windowOffsetMinutes must be between 0 and 720.";
        }

        return null;
    }

    /// <summary>
    /// Inserts a new GlobalParameter row if the key does not exist, otherwise
    /// updates its Value and LastUpdateDate. Tracks new rows in the change
    /// tracker so SaveChangesAsync persists them.
    /// </summary>
    private void UpsertParameter(
        List<GlobalParameter> existingParams,
        string key,
        string value,
        DateTime now)
    {
        var existing = existingParams.FirstOrDefault(p => p.Key == key);
        if (existing is null)
        {
            var newParam = new GlobalParameter
            {
                Key          = key,
                Value        = value,
                CreatedBy    = "admin-api",
                CreationDate = now,
                LastUpdateDate = now
            };
            _db.GlobalParameters.Add(newParam);
            existingParams.Add(newParam);
        }
        else
        {
            existing.Value         = value;
            existing.LastUpdateDate = now;
        }
    }
}
