using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.RecurringPerformance;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.RecurringPerformance.GetSettings;

/// <summary>
/// Returns all §10.7 high-volume tunables as a structured grouped payload.
/// Reads GlobalParameter rows by the keys seeded in RecurringBillingSeeder.
/// Always returns all 7 perGateway rows in the canonical order: NmiSpreedly,
/// NmiDirect, CheckoutEUR, CheckoutUS, CheckoutUsLlc, Shift4, StripeEms.
/// </summary>
public class GetRecurringPerformanceSettingsHandler
    : IRequestHandler<GetRecurringPerformanceSettingsQuery, Result<RecurringPerformanceSettingsDto>>
{
    private readonly AppDbContext _db;

    // Canonical processor order for the perGateway array.
    public static readonly CardProcessor[] ProcessorOrder =
    {
        CardProcessor.NmiSpreedly,
        CardProcessor.NmiDirect,
        CardProcessor.CheckoutEUR,
        CardProcessor.CheckoutUS,
        CardProcessor.CheckoutUsLlc,
        CardProcessor.Shift4,
        CardProcessor.StripeEms
    };

    // The only values advertised in the supported-* arrays today.
    private static readonly List<string> SupportedCascadeStrategies      = new() { "DeferredUplineRollup" };
    private static readonly List<string> SupportedAggregatorTriggerModes = new() { "AfterAllChargeWorkers" };

    // Default values mirror the seeder defaults (§10.7).
    private const int    DefaultWindowHours     = 3;
    private const string DefaultBatchStart      = "05:00";
    private const int    DefaultMinWorkers       = 2;
    private const int    DefaultMaxConcurrency   = 10;
    private const int    DefaultWindowOffset     = 0;
    private const int    DefaultLatencySampling  = 14;
    private const string DefaultCascadeStrategy  = "DeferredUplineRollup";
    private const string DefaultAggregatorMode   = "AfterAllChargeWorkers";

    public GetRecurringPerformanceSettingsHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<RecurringPerformanceSettingsDto>> Handle(
        GetRecurringPerformanceSettingsQuery request, CancellationToken cancellationToken)
    {
        var parameters = await _db.GlobalParameters
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var window = new WindowSettingsDto
        {
            TargetCompletionWindowHours = GetInt(parameters, "RecurringBilling:TargetCompletionWindowHours", DefaultWindowHours),
            BatchStartTimeUtc           = GetString(parameters, "RecurringBilling:BatchStartTimeUtc", DefaultBatchStart)
        };

        var perGateway = ProcessorOrder
            .Select(processor =>
            {
                var name = processor.ToString();
                return new GatewayPerformanceRowDto
                {
                    Processor           = name,
                    MinWorkers          = GetInt(parameters, $"RecurringBilling:MinWorkersPerGateway:{name}", DefaultMinWorkers),
                    MaxConcurrency      = GetInt(parameters, $"RecurringBilling:MaxConcurrencyPerGateway:{name}", DefaultMaxConcurrency),
                    WindowOffsetMinutes = GetInt(parameters, $"RecurringBilling:GatewayWindowOffsetMinutes:{name}", DefaultWindowOffset)
                };
            })
            .ToList();

        var dto = new RecurringPerformanceSettingsDto
        {
            Window                          = window,
            PerGateway                      = perGateway,
            LatencySamplingDays             = GetInt(parameters, "RecurringBilling:LatencySamplingDays", DefaultLatencySampling),
            CascadeStrategy                 = GetString(parameters, "RecurringBilling:CascadeStrategy", DefaultCascadeStrategy),
            AggregatorTriggerMode           = GetString(parameters, "RecurringBilling:AggregatorTriggerMode", DefaultAggregatorMode),
            SupportedCascadeStrategies      = SupportedCascadeStrategies,
            SupportedAggregatorTriggerModes = SupportedAggregatorTriggerModes
        };

        return Result<RecurringPerformanceSettingsDto>.Success(dto);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static int GetInt(IEnumerable<GlobalParameter> parameters, string key, int defaultValue)
    {
        var param = parameters.FirstOrDefault(p => p.Key == key);
        return param is not null && int.TryParse(param.Value, out var v) ? v : defaultValue;
    }

    private static string GetString(IEnumerable<GlobalParameter> parameters, string key, string defaultValue)
    {
        var param = parameters.FirstOrDefault(p => p.Key == key);
        return param?.Value ?? defaultValue;
    }
}
