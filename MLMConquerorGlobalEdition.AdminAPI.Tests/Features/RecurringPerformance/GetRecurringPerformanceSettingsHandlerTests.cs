using MLMConquerorGlobalEdition.AdminAPI.Features.RecurringPerformance.GetSettings;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.RecurringPerformance;

/// <summary>
/// Unit tests for GetRecurringPerformanceSettingsHandler.
/// Verifies that all 24 GlobalParameter keys are read correctly and that
/// the supported* arrays are always present in the response.
/// </summary>
public class GetRecurringPerformanceSettingsHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 13, 0, 0, 0, DateTimeKind.Utc);

    private static GlobalParameter MakeParam(string key, string value) => new()
    {
        Key          = key,
        Value        = value,
        CreatedBy    = "seed",
        CreationDate = FixedNow,
        LastUpdateDate = FixedNow
    };

    // ── Round-trip: all 24 keys seeded, all 24 values returned ────────────────

    [Fact]
    public async Task Handle_WhenAllParamsSeeded_ReturnsExactValues()
    {
        await using var db = InMemoryDbHelper.Create();

        // Seed window params
        db.GlobalParameters.AddRange(
            MakeParam("RecurringBilling:TargetCompletionWindowHours", "4"),
            MakeParam("RecurringBilling:BatchStartTimeUtc", "06:00"),
            MakeParam("RecurringBilling:LatencySamplingDays", "7"),
            MakeParam("RecurringBilling:CascadeStrategy", "DeferredUplineRollup"),
            MakeParam("RecurringBilling:AggregatorTriggerMode", "AfterAllChargeWorkers")
        );

        // Seed per-processor params for all 7 processors
        foreach (var proc in GetRecurringPerformanceSettingsHandler.ProcessorOrder)
        {
            var name = proc.ToString();
            db.GlobalParameters.AddRange(
                MakeParam($"RecurringBilling:MinWorkersPerGateway:{name}", "3"),
                MakeParam($"RecurringBilling:MaxConcurrencyPerGateway:{name}", "15"),
                MakeParam($"RecurringBilling:GatewayWindowOffsetMinutes:{name}", "10")
            );
        }

        await db.SaveChangesAsync();

        var handler = new GetRecurringPerformanceSettingsHandler(db);
        var result  = await handler.Handle(new GetRecurringPerformanceSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value!;

        // Window params
        dto.Window.TargetCompletionWindowHours.Should().Be(4);
        dto.Window.BatchStartTimeUtc.Should().Be("06:00");

        // Scalar params
        dto.LatencySamplingDays.Should().Be(7);
        dto.CascadeStrategy.Should().Be("DeferredUplineRollup");
        dto.AggregatorTriggerMode.Should().Be("AfterAllChargeWorkers");

        // All 7 perGateway rows present with seeded values
        dto.PerGateway.Should().HaveCount(7);
        foreach (var row in dto.PerGateway)
        {
            row.MinWorkers.Should().Be(3,          because: $"{row.Processor} MinWorkers");
            row.MaxConcurrency.Should().Be(15,     because: $"{row.Processor} MaxConcurrency");
            row.WindowOffsetMinutes.Should().Be(10, because: $"{row.Processor} WindowOffsetMinutes");
        }
    }

    // ── Default fallbacks when no rows seeded ─────────────────────────────────

    [Fact]
    public async Task Handle_WhenNoParamsSeeded_ReturnsDefaults()
    {
        await using var db = InMemoryDbHelper.Create();

        var handler = new GetRecurringPerformanceSettingsHandler(db);
        var result  = await handler.Handle(new GetRecurringPerformanceSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value!;

        dto.Window.TargetCompletionWindowHours.Should().Be(3);
        dto.Window.BatchStartTimeUtc.Should().Be("05:00");
        dto.LatencySamplingDays.Should().Be(14);
        dto.CascadeStrategy.Should().Be("DeferredUplineRollup");
        dto.AggregatorTriggerMode.Should().Be("AfterAllChargeWorkers");

        dto.PerGateway.Should().HaveCount(7);
        foreach (var row in dto.PerGateway)
        {
            row.MinWorkers.Should().Be(2);
            row.MaxConcurrency.Should().Be(10);
            row.WindowOffsetMinutes.Should().Be(0);
        }
    }

    // ── Canonical perGateway ordering ─────────────────────────────────────────

    [Fact]
    public async Task Handle_PerGatewayRows_AreInCanonicalOrder()
    {
        await using var db = InMemoryDbHelper.Create();

        var handler = new GetRecurringPerformanceSettingsHandler(db);
        var result  = await handler.Handle(new GetRecurringPerformanceSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var processors = result.Value!.PerGateway.Select(r => r.Processor).ToList();

        processors.Should().ContainInOrder(
            "NmiSpreedly",
            "NmiDirect",
            "CheckoutEUR",
            "CheckoutUS",
            "CheckoutUsLlc",
            "Shift4",
            "StripeEms");
    }

    // ── Supported arrays are always present ───────────────────────────────────

    [Fact]
    public async Task Handle_AlwaysReturnsSupportedCascadeStrategies()
    {
        await using var db = InMemoryDbHelper.Create();

        var handler = new GetRecurringPerformanceSettingsHandler(db);
        var result  = await handler.Handle(new GetRecurringPerformanceSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SupportedCascadeStrategies.Should().Contain("DeferredUplineRollup");
    }

    [Fact]
    public async Task Handle_AlwaysReturnsSupportedAggregatorTriggerModes()
    {
        await using var db = InMemoryDbHelper.Create();

        var handler = new GetRecurringPerformanceSettingsHandler(db);
        var result  = await handler.Handle(new GetRecurringPerformanceSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SupportedAggregatorTriggerModes.Should().Contain("AfterAllChargeWorkers");
    }

    // ── Processor enum names match exactly ────────────────────────────────────

    [Fact]
    public async Task Handle_ProcessorNames_UseEnumNameString()
    {
        await using var db = InMemoryDbHelper.Create();

        var handler = new GetRecurringPerformanceSettingsHandler(db);
        var result  = await handler.Handle(new GetRecurringPerformanceSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var names = result.Value!.PerGateway.Select(r => r.Processor).ToList();

        // Verify each name matches the CardProcessor enum name (JsonStringEnumConverter format)
        var expectedNames = GetRecurringPerformanceSettingsHandler.ProcessorOrder
            .Select(p => p.ToString())
            .ToList();
        var actualNames = result.Value!.PerGateway.Select(r => r.Processor).ToList();
        actualNames.Should().Equal(expectedNames);
    }

    // ── Mixed state: some params seeded, some not ─────────────────────────────

    [Fact]
    public async Task Handle_WhenSomeParamsSeeded_UsesSeededValuesAndDefaultsForRest()
    {
        await using var db = InMemoryDbHelper.Create();

        // Only seed the window hours; everything else falls back to defaults
        db.GlobalParameters.Add(MakeParam("RecurringBilling:TargetCompletionWindowHours", "6"));
        await db.SaveChangesAsync();

        var handler = new GetRecurringPerformanceSettingsHandler(db);
        var result  = await handler.Handle(new GetRecurringPerformanceSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Window.TargetCompletionWindowHours.Should().Be(6);
        result.Value.Window.BatchStartTimeUtc.Should().Be("05:00"); // default
        result.Value.LatencySamplingDays.Should().Be(14);           // default
    }
}
