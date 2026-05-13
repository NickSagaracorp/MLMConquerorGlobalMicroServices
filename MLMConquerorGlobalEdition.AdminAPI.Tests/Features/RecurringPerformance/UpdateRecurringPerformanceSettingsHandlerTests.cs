using MLMConquerorGlobalEdition.AdminAPI.DTOs.RecurringPerformance;
using MLMConquerorGlobalEdition.AdminAPI.Features.RecurringPerformance.GetSettings;
using MLMConquerorGlobalEdition.AdminAPI.Features.RecurringPerformance.UpdateSettings;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.RecurringPerformance;

/// <summary>
/// Unit tests for UpdateRecurringPerformanceSettingsHandler.
/// Covers every validation rule with a negative and a happy-path positive case.
/// </summary>
public class UpdateRecurringPerformanceSettingsHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 13, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> DateTimeMock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    /// <summary>Builds a valid request with all 7 processors present.</summary>
    private static UpdateRecurringPerformanceSettingsRequest ValidRequest(
        int windowHours = 3,
        string batchStart = "05:00",
        int latencySampling = 14,
        string cascadeStrategy = "DeferredUplineRollup",
        string aggregatorMode = "AfterAllChargeWorkers",
        int minWorkers = 2,
        int maxConcurrency = 10,
        int windowOffset = 0)
    {
        var perGateway = GetRecurringPerformanceSettingsHandler.ProcessorOrder
            .Select(p => new GatewayPerformanceRowDto
            {
                Processor           = p.ToString(),
                MinWorkers          = minWorkers,
                MaxConcurrency      = maxConcurrency,
                WindowOffsetMinutes = windowOffset
            })
            .ToList();

        return new UpdateRecurringPerformanceSettingsRequest
        {
            Window = new WindowSettingsDto
            {
                TargetCompletionWindowHours = windowHours,
                BatchStartTimeUtc           = batchStart
            },
            PerGateway            = perGateway,
            LatencySamplingDays   = latencySampling,
            CascadeStrategy       = cascadeStrategy,
            AggregatorTriggerMode = aggregatorMode
        };
    }

    // ── Positive case ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidRequest_PersistsAllRowsAndReturnsDto()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UpdateRecurringPerformanceSettingsHandler(db, DateTimeMock().Object);

        var result = await handler.Handle(
            new UpdateRecurringPerformanceSettingsCommand(ValidRequest(windowHours: 5, latencySampling: 30)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Window.TargetCompletionWindowHours.Should().Be(5);
        result.Value.LatencySamplingDays.Should().Be(30);
        result.Value.PerGateway.Should().HaveCount(7);

        // Confirm DB was updated (5 scalar + 7×3 = 26 rows)
        db.GlobalParameters.Should().HaveCountGreaterThanOrEqualTo(26);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsConfirmedPerGatewayValues()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UpdateRecurringPerformanceSettingsHandler(db, DateTimeMock().Object);

        var result = await handler.Handle(
            new UpdateRecurringPerformanceSettingsCommand(ValidRequest(minWorkers: 5, maxConcurrency: 20, windowOffset: 45)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        foreach (var row in result.Value!.PerGateway)
        {
            row.MinWorkers.Should().Be(5);
            row.MaxConcurrency.Should().Be(20);
            row.WindowOffsetMinutes.Should().Be(45);
        }
    }

    // ── targetCompletionWindowHours validation ────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(25)]
    [InlineData(100)]
    public async Task Handle_InvalidWindowHours_ReturnsValidationFailure(int hours)
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UpdateRecurringPerformanceSettingsHandler(db, DateTimeMock().Object);

        var result = await handler.Handle(
            new UpdateRecurringPerformanceSettingsCommand(ValidRequest(windowHours: hours)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION");
        result.Error.Should().Contain("targetCompletionWindowHours");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(24)]
    public async Task Handle_ValidWindowHours_Succeeds(int hours)
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UpdateRecurringPerformanceSettingsHandler(db, DateTimeMock().Object);

        var result = await handler.Handle(
            new UpdateRecurringPerformanceSettingsCommand(ValidRequest(windowHours: hours)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ── batchStartTimeUtc validation ──────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("5:00")]
    [InlineData("25:00")]
    [InlineData("23:60")]
    [InlineData("1200")]
    [InlineData("noon")]
    public async Task Handle_InvalidBatchStartTime_ReturnsValidationFailure(string batchStart)
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UpdateRecurringPerformanceSettingsHandler(db, DateTimeMock().Object);

        var result = await handler.Handle(
            new UpdateRecurringPerformanceSettingsCommand(ValidRequest(batchStart: batchStart)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION");
        result.Error.Should().Contain("batchStartTimeUtc");
    }

    [Theory]
    [InlineData("00:00")]
    [InlineData("05:00")]
    [InlineData("12:30")]
    [InlineData("23:59")]
    public async Task Handle_ValidBatchStartTime_Succeeds(string batchStart)
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UpdateRecurringPerformanceSettingsHandler(db, DateTimeMock().Object);

        var result = await handler.Handle(
            new UpdateRecurringPerformanceSettingsCommand(ValidRequest(batchStart: batchStart)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ── latencySamplingDays validation ────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(91)]
    [InlineData(365)]
    public async Task Handle_InvalidLatencySamplingDays_ReturnsValidationFailure(int days)
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UpdateRecurringPerformanceSettingsHandler(db, DateTimeMock().Object);

        var result = await handler.Handle(
            new UpdateRecurringPerformanceSettingsCommand(ValidRequest(latencySampling: days)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION");
        result.Error.Should().Contain("latencySamplingDays");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(14)]
    [InlineData(90)]
    public async Task Handle_ValidLatencySamplingDays_Succeeds(int days)
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UpdateRecurringPerformanceSettingsHandler(db, DateTimeMock().Object);

        var result = await handler.Handle(
            new UpdateRecurringPerformanceSettingsCommand(ValidRequest(latencySampling: days)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ── cascadeStrategy validation ────────────────────────────────────────────

    [Theory]
    [InlineData("Unknown")]
    [InlineData("Streaming")]
    [InlineData("")]
    public async Task Handle_InvalidCascadeStrategy_ReturnsValidationFailure(string strategy)
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UpdateRecurringPerformanceSettingsHandler(db, DateTimeMock().Object);

        var result = await handler.Handle(
            new UpdateRecurringPerformanceSettingsCommand(ValidRequest(cascadeStrategy: strategy)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION");
        result.Error.Should().Contain("cascadeStrategy");
    }

    // ── aggregatorTriggerMode validation ─────────────────────────────────────

    [Theory]
    [InlineData("Streaming")]
    [InlineData("Manual")]
    [InlineData("")]
    public async Task Handle_InvalidAggregatorTriggerMode_ReturnsValidationFailure(string mode)
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UpdateRecurringPerformanceSettingsHandler(db, DateTimeMock().Object);

        var result = await handler.Handle(
            new UpdateRecurringPerformanceSettingsCommand(ValidRequest(aggregatorMode: mode)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION");
        result.Error.Should().Contain("aggregatorTriggerMode");
    }

    // ── perGateway row-level validation ──────────────────────────────────────

    [Fact]
    public async Task Handle_MinWorkersLessThan1_ReturnsValidationFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UpdateRecurringPerformanceSettingsHandler(db, DateTimeMock().Object);

        var result = await handler.Handle(
            new UpdateRecurringPerformanceSettingsCommand(ValidRequest(minWorkers: 0)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION");
        result.Error.Should().Contain("minWorkers");
    }

    [Fact]
    public async Task Handle_MaxConcurrencyLessThanMinWorkers_ReturnsValidationFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UpdateRecurringPerformanceSettingsHandler(db, DateTimeMock().Object);

        // minWorkers=5, maxConcurrency=3 → invalid
        var result = await handler.Handle(
            new UpdateRecurringPerformanceSettingsCommand(ValidRequest(minWorkers: 5, maxConcurrency: 3)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION");
        result.Error.Should().Contain("maxConcurrency");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(721)]
    [InlineData(1000)]
    public async Task Handle_WindowOffsetOutOfRange_ReturnsValidationFailure(int offset)
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UpdateRecurringPerformanceSettingsHandler(db, DateTimeMock().Object);

        var result = await handler.Handle(
            new UpdateRecurringPerformanceSettingsCommand(ValidRequest(windowOffset: offset)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION");
        result.Error.Should().Contain("windowOffsetMinutes");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(360)]
    [InlineData(720)]
    public async Task Handle_WindowOffsetAtBoundaries_Succeeds(int offset)
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UpdateRecurringPerformanceSettingsHandler(db, DateTimeMock().Object);

        var result = await handler.Handle(
            new UpdateRecurringPerformanceSettingsCommand(ValidRequest(windowOffset: offset)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ── Missing or extra processor rows ───────────────────────────────────────

    [Fact]
    public async Task Handle_MissingProcessorRow_ReturnsValidationFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UpdateRecurringPerformanceSettingsHandler(db, DateTimeMock().Object);

        // Build a request missing StripeEms
        var req = ValidRequest();
        req.PerGateway.RemoveAll(r => r.Processor == "StripeEms");

        var result = await handler.Handle(
            new UpdateRecurringPerformanceSettingsCommand(req),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION");
        result.Error.Should().Contain("StripeEms");
    }

    [Fact]
    public async Task Handle_ExtraProcessorRow_ReturnsValidationFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UpdateRecurringPerformanceSettingsHandler(db, DateTimeMock().Object);

        var req = ValidRequest();
        req.PerGateway.Add(new GatewayPerformanceRowDto
        {
            Processor = "UnknownGateway", MinWorkers = 1, MaxConcurrency = 2, WindowOffsetMinutes = 0
        });

        var result = await handler.Handle(
            new UpdateRecurringPerformanceSettingsCommand(req),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION");
        result.Error.Should().Contain("UnknownGateway");
    }

    [Fact]
    public async Task Handle_EmptyPerGatewayList_ReturnsValidationFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UpdateRecurringPerformanceSettingsHandler(db, DateTimeMock().Object);

        var req = ValidRequest();
        req.PerGateway.Clear();

        var result = await handler.Handle(
            new UpdateRecurringPerformanceSettingsCommand(req),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION");
    }

    // ── Idempotent upsert (update existing rows) ──────────────────────────────

    [Fact]
    public async Task Handle_WhenParamsAlreadyExist_UpdatesThemInPlace()
    {
        await using var db = InMemoryDbHelper.Create();

        // Pre-seed the window hours param
        db.GlobalParameters.Add(new Domain.Entities.General.GlobalParameter
        {
            Key = "RecurringBilling:TargetCompletionWindowHours",
            Value = "3",
            CreatedBy = "seed",
            CreationDate = FixedNow,
            LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        var handler = new UpdateRecurringPerformanceSettingsHandler(db, DateTimeMock().Object);

        var result = await handler.Handle(
            new UpdateRecurringPerformanceSettingsCommand(ValidRequest(windowHours: 8)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Window.TargetCompletionWindowHours.Should().Be(8);

        // Should still be only one row for this key (no duplicates)
        db.GlobalParameters.Count(p => p.Key == "RecurringBilling:TargetCompletionWindowHours")
            .Should().Be(1);
    }

    // ── IDateTimeProvider usage ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NewParam_UsesIDateTimeProviderForTimestamps()
    {
        await using var db = InMemoryDbHelper.Create();
        var clockMock = DateTimeMock();
        var handler   = new UpdateRecurringPerformanceSettingsHandler(db, clockMock.Object);

        await handler.Handle(
            new UpdateRecurringPerformanceSettingsCommand(ValidRequest()),
            CancellationToken.None);

        // All new rows should have CreationDate == FixedNow
        db.GlobalParameters.All(p => p.CreationDate == FixedNow || p.LastUpdateDate == FixedNow)
            .Should().BeTrue();
    }
}
