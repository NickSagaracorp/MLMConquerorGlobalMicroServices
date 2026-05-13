using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;
using Microsoft.Extensions.Logging;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

/// <summary>
/// Unit tests for RecurringBillingPlanner.PreviewAsync (dry-run mode).
/// Verifies case counting, floor/ceiling clamping, latency sourcing,
/// and that no database rows are written.
/// </summary>
public class RecurringBillingPlannerPreviewTests
{
    private static readonly DateTime TestDate = new(2026, 5, 13);

    private static ILogger<RecurringBillingPlanner> Logger()
        => new Mock<ILogger<RecurringBillingPlanner>>().Object;

    private static IDateTimeProvider DateTimeMock(DateTime? now = null)
    {
        var mock = new Mock<IDateTimeProvider>();
        mock.Setup(d => d.Now).Returns(now ?? TestDate);
        return mock.Object;
    }

    private static IGatewayRouter RouterMock(CardProcessor returnProcessor = CardProcessor.NmiSpreedly)
    {
        var mock = new Mock<IGatewayRouter>();
        var routePlan = new GatewayRoutingPlan
        {
            RouteBucketKey = "test-bucket",
            Steps = new List<GatewayAttemptPlan>
            {
                new GatewayAttemptPlan { CardProcessor = returnProcessor, FallbackStepIndex = 0 }
            }
        };
        mock.Setup(r => r.ResolveAsync(It.IsAny<GatewayRoutingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GatewayRoutingPlan>.Success(routePlan));
        return mock.Object;
    }

    private static SubscriptionBillingState MakeState(long shardKey, string memberId, DateTime nextAttempt,
        RecurringBillingStatus status = RecurringBillingStatus.Active)
        => new SubscriptionBillingState
        {
            Id                       = Guid.NewGuid().ToString(),
            MemberId                 = memberId,
            MembershipSubscriptionId = Guid.NewGuid().ToString(),
            RecurringBillingPlanId   = 1,
            BillingAnchorDate        = nextAttempt.AddMonths(-1),
            NextBillingDate          = nextAttempt,
            NextAttemptDate          = nextAttempt,
            Status                   = status,
            ShardKey                 = shardKey,
            CreatedBy                = "test",
            CreationDate             = TestDate,
            LastUpdateDate           = TestDate
        };

    private static MemberProfile MakeProfile(string memberId, string country = "US")
        => new MemberProfile
        {
            Id             = Guid.NewGuid().ToString(),
            MemberId       = memberId,
            Country        = country,
            UserId         = Guid.NewGuid(),
            CreatedBy      = "test",
            CreationDate   = TestDate,
            LastUpdateDate = TestDate
        };

    private static GatewayChargeAttempt MakeAttempt(CardProcessor processor, long latencyMs, DateTime? at = null)
        => new GatewayChargeAttempt
        {
            RouteBucketKey   = "bucket",
            CardProcessor    = processor,
            PresentmentCurrency = "USD",
            OriginalAmountUsd = 99m,
            ConvertedAmount  = 99m,
            Outcome          = "Success",
            AttemptedAtUtc   = at ?? TestDate.AddDays(-1),
            MemberId         = "test-member",
            OperationType    = BillingOperationType.Payment,
            CardBrand        = CardBrand.Visa,
            LatencyMs        = latencyMs,
            CreatedBy        = "test",
            CreationDate     = at ?? TestDate.AddDays(-1)
        };

    // ── No due states → empty result, no rows written ────────────────────────

    [Fact]
    public async Task PreviewAsync_WhenNoDueStates_ReturnsEmptyPreviewAndWritesNoRows()
    {
        using var db = TestDbContextFactory.Create();
        var planner  = new RecurringBillingPlanner(db, RouterMock(), DateTimeMock(), Logger());

        var result = await planner.PreviewAsync(TestDate);

        result.IsSuccess.Should().BeTrue();
        result.Value!.PendingBills.Should().Be(0);
        result.Value.PerGateway.Should().BeEmpty();
        result.Value.WithinTarget.Should().BeTrue();

        // Confirm no batch rows were written
        (await db.RecurringBillingBatches.CountAsync()).Should().Be(0);
        (await db.RecurringBillingBatchShards.CountAsync()).Should().Be(0);
    }

    // ── PendingBills counts all three eligible statuses ───────────────────────

    [Fact]
    public async Task PreviewAsync_PendingBills_CountsActiveRetryingAndAwaitingAnniversary()
    {
        using var db = TestDbContextFactory.Create();

        db.MemberProfiles.AddRange(
            MakeProfile("m1"), MakeProfile("m2"), MakeProfile("m3"), MakeProfile("m4"));
        db.SubscriptionBillingStates.AddRange(
            MakeState(1, "m1", TestDate, RecurringBillingStatus.Active),
            MakeState(2, "m2", TestDate, RecurringBillingStatus.Retrying),
            MakeState(3, "m3", TestDate, RecurringBillingStatus.AwaitingAnniversaryRetry),
            MakeState(4, "m4", TestDate, RecurringBillingStatus.Stopped) // excluded
        );
        await db.SaveChangesAsync();

        var planner = new RecurringBillingPlanner(db, RouterMock(), DateTimeMock(), Logger());
        var result  = await planner.PreviewAsync(TestDate);

        result.IsSuccess.Should().BeTrue();
        result.Value!.PendingBills.Should().Be(3); // Stopped excluded
    }

    // ── Future NextAttemptDate excluded ──────────────────────────────────────

    [Fact]
    public async Task PreviewAsync_FutureAttemptDate_IsNotCounted()
    {
        using var db = TestDbContextFactory.Create();

        db.MemberProfiles.AddRange(MakeProfile("m1"), MakeProfile("m2"));
        db.SubscriptionBillingStates.AddRange(
            MakeState(1, "m1", TestDate),             // due today
            MakeState(2, "m2", TestDate.AddDays(1))   // due tomorrow — excluded
        );
        await db.SaveChangesAsync();

        var planner = new RecurringBillingPlanner(db, RouterMock(), DateTimeMock(), Logger());
        var result  = await planner.PreviewAsync(TestDate);

        result.IsSuccess.Should().BeTrue();
        result.Value!.PendingBills.Should().Be(1);
    }

    // ── Floor clamping ────────────────────────────────────────────────────────

    [Fact]
    public async Task PreviewAsync_WhenWorkersNeededBelowFloor_HitFloorIsTrue()
    {
        using var db = TestDbContextFactory.Create();

        // Seed a high minWorkers so the formula naturally gives fewer workers
        db.GlobalParameters.Add(new GlobalParameter
        {
            Key = "RecurringBilling:MinWorkersPerGateway:NmiSpreedly",
            Value = "10",   // high floor
            CreatedBy = "test", CreationDate = TestDate, LastUpdateDate = TestDate
        });
        db.GlobalParameters.Add(new GlobalParameter
        {
            Key = "RecurringBilling:MaxConcurrencyPerGateway:NmiSpreedly",
            Value = "20",
            CreatedBy = "test", CreationDate = TestDate, LastUpdateDate = TestDate
        });
        // Very fast latency + tiny window → raw workers = 1, but floor=10
        db.GlobalParameters.Add(new GlobalParameter
        {
            Key = "RecurringBilling:TargetCompletionWindowHours",
            Value = "3",
            CreatedBy = "test", CreationDate = TestDate, LastUpdateDate = TestDate
        });

        // 1 case, tiny latency — raw = ceil(1 × 1ms / 3h_ms) = 1; floor kicks in to 10
        db.MemberProfiles.Add(MakeProfile("m1"));
        db.SubscriptionBillingStates.Add(MakeState(1, "m1", TestDate));
        db.GatewayChargeAttempts.Add(MakeAttempt(CardProcessor.NmiSpreedly, 1));
        await db.SaveChangesAsync();

        var planner = new RecurringBillingPlanner(db, RouterMock(CardProcessor.NmiSpreedly), DateTimeMock(), Logger());
        var result  = await planner.PreviewAsync(TestDate);

        result.IsSuccess.Should().BeTrue();
        var row = result.Value!.PerGateway.Single(r => r.Processor == "NmiSpreedly");
        row.HitFloor.Should().BeTrue();
        row.HitCeiling.Should().BeFalse();
        row.WorkersNeeded.Should().Be(10);
    }

    // ── Ceiling clamping ──────────────────────────────────────────────────────

    [Fact]
    public async Task PreviewAsync_WhenWorkersNeededAboveCeiling_HitCeilingIsTrue()
    {
        using var db = TestDbContextFactory.Create();

        // Low ceiling = 2, short window (1 hour), high case load → ceiling clamps.
        // Formula: raw = ceil(cases × latencyMs / windowMs)
        //   = ceil(1500 × 5000 / 3_600_000) = ceil(2.083) = 3  → clamped to 2
        db.GlobalParameters.Add(new GlobalParameter
        {
            Key = "RecurringBilling:MaxConcurrencyPerGateway:NmiSpreedly",
            Value = "2",
            CreatedBy = "test", CreationDate = TestDate, LastUpdateDate = TestDate
        });
        db.GlobalParameters.Add(new GlobalParameter
        {
            Key = "RecurringBilling:MinWorkersPerGateway:NmiSpreedly",
            Value = "1",
            CreatedBy = "test", CreationDate = TestDate, LastUpdateDate = TestDate
        });
        db.GlobalParameters.Add(new GlobalParameter
        {
            Key = "RecurringBilling:TargetCompletionWindowHours",
            Value = "1",   // 1 hour window keeps denominator small
            CreatedBy = "test", CreationDate = TestDate, LastUpdateDate = TestDate
        });

        // 1500 cases with 5000ms avg latency → raw=3, clamped to 2
        for (int i = 1; i <= 1500; i++)
        {
            var mid = $"member-{i}";
            db.MemberProfiles.Add(MakeProfile(mid));
            db.SubscriptionBillingStates.Add(MakeState(i, mid, TestDate));
        }
        db.GatewayChargeAttempts.Add(MakeAttempt(CardProcessor.NmiSpreedly, 5000));
        await db.SaveChangesAsync();

        var planner = new RecurringBillingPlanner(db, RouterMock(CardProcessor.NmiSpreedly), DateTimeMock(), Logger());
        var result  = await planner.PreviewAsync(TestDate);

        result.IsSuccess.Should().BeTrue();
        var row = result.Value!.PerGateway.Single(r => r.Processor == "NmiSpreedly");
        row.HitCeiling.Should().BeTrue();
        row.HitFloor.Should().BeFalse();
        row.WorkersNeeded.Should().Be(2);
    }

    // ── AvgLatencyMs from GatewayChargeAttempt ────────────────────────────────

    [Fact]
    public async Task PreviewAsync_UsesAvgLatencyFromGatewayChargeAttempt()
    {
        using var db = TestDbContextFactory.Create();

        db.MemberProfiles.Add(MakeProfile("m1"));
        db.SubscriptionBillingStates.Add(MakeState(1, "m1", TestDate));

        // Two attempts: avg should be (1000+3000)/2 = 2000ms
        db.GatewayChargeAttempts.AddRange(
            MakeAttempt(CardProcessor.NmiSpreedly, 1000, TestDate.AddDays(-1)),
            MakeAttempt(CardProcessor.NmiSpreedly, 3000, TestDate.AddDays(-2))
        );
        await db.SaveChangesAsync();

        var planner = new RecurringBillingPlanner(db, RouterMock(CardProcessor.NmiSpreedly), DateTimeMock(), Logger());
        var result  = await planner.PreviewAsync(TestDate);

        result.IsSuccess.Should().BeTrue();
        var row = result.Value!.PerGateway.Single(r => r.Processor == "NmiSpreedly");
        row.AvgLatencyMs.Should().Be(2000);
    }

    // ── Default latency when no GatewayChargeAttempt rows exist ──────────────

    [Fact]
    public async Task PreviewAsync_WhenNoLatencyData_UsesDefaultAndAddsNote()
    {
        using var db = TestDbContextFactory.Create();

        db.MemberProfiles.Add(MakeProfile("m1"));
        db.SubscriptionBillingStates.Add(MakeState(1, "m1", TestDate));
        // No GatewayChargeAttempt rows
        await db.SaveChangesAsync();

        var planner = new RecurringBillingPlanner(db, RouterMock(CardProcessor.NmiSpreedly), DateTimeMock(), Logger());
        var result  = await planner.PreviewAsync(TestDate);

        result.IsSuccess.Should().BeTrue();
        var row = result.Value!.PerGateway.Single();
        row.AvgLatencyMs.Should().BeGreaterThan(0);  // default is used
        result.Value.Notes.Should().Contain(n => n.Contains("NmiSpreedly"));
    }

    // ── No database rows written (dry-run guarantee) ─────────────────────────

    [Fact]
    public async Task PreviewAsync_WritesNoBatchOrShardRows()
    {
        using var db = TestDbContextFactory.Create();

        for (int i = 1; i <= 10; i++)
        {
            var mid = $"member-{i}";
            db.MemberProfiles.Add(MakeProfile(mid));
            db.SubscriptionBillingStates.Add(MakeState(i, mid, TestDate));
        }
        await db.SaveChangesAsync();

        var planner = new RecurringBillingPlanner(db, RouterMock(), DateTimeMock(), Logger());
        await planner.PreviewAsync(TestDate);

        (await db.RecurringBillingBatches.CountAsync()).Should().Be(0);
        (await db.RecurringBillingBatchShards.CountAsync()).Should().Be(0);
    }

    // ── Processors with 0 cases are omitted from perGateway ──────────────────

    [Fact]
    public async Task PreviewAsync_ProcessorsWithZeroCases_AreOmitted()
    {
        using var db = TestDbContextFactory.Create();

        db.MemberProfiles.Add(MakeProfile("m1"));
        db.SubscriptionBillingStates.Add(MakeState(1, "m1", TestDate));
        await db.SaveChangesAsync();

        // All cases route to NmiSpreedly
        var planner = new RecurringBillingPlanner(db, RouterMock(CardProcessor.NmiSpreedly), DateTimeMock(), Logger());
        var result  = await planner.PreviewAsync(TestDate);

        result.IsSuccess.Should().BeTrue();
        // Only NmiSpreedly has cases; other 6 processors should not appear
        result.Value!.PerGateway.Should().HaveCount(1);
        result.Value.PerGateway.Single().Processor.Should().Be("NmiSpreedly");
    }

    // ── withinTarget flag ─────────────────────────────────────────────────────

    [Fact]
    public async Task PreviewAsync_WithinTarget_IsTrueWhenCompletionFitsInWindow()
    {
        using var db = TestDbContextFactory.Create();

        db.GlobalParameters.Add(new GlobalParameter
        {
            Key = "RecurringBilling:TargetCompletionWindowHours", Value = "3",
            CreatedBy = "test", CreationDate = TestDate, LastUpdateDate = TestDate
        });

        db.MemberProfiles.Add(MakeProfile("m1"));
        db.SubscriptionBillingStates.Add(MakeState(1, "m1", TestDate));
        // 1 case × 1ms latency / (2 workers × 60000) ≈ 0 minutes → within 3h
        db.GatewayChargeAttempts.Add(MakeAttempt(CardProcessor.NmiSpreedly, 1));
        await db.SaveChangesAsync();

        var planner = new RecurringBillingPlanner(db, RouterMock(CardProcessor.NmiSpreedly), DateTimeMock(), Logger());
        var result  = await planner.PreviewAsync(TestDate);

        result.IsSuccess.Should().BeTrue();
        result.Value!.WithinTarget.Should().BeTrue();
    }

    // ── TargetWindowHours from GlobalParameter ────────────────────────────────

    [Fact]
    public async Task PreviewAsync_TargetWindowHours_ReflectsSeededParameter()
    {
        using var db = TestDbContextFactory.Create();

        db.GlobalParameters.Add(new GlobalParameter
        {
            Key = "RecurringBilling:TargetCompletionWindowHours", Value = "5",
            CreatedBy = "test", CreationDate = TestDate, LastUpdateDate = TestDate
        });
        await db.SaveChangesAsync();

        var planner = new RecurringBillingPlanner(db, RouterMock(), DateTimeMock(), Logger());
        var result  = await planner.PreviewAsync(TestDate);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TargetWindowHours.Should().Be(5);
    }
}
