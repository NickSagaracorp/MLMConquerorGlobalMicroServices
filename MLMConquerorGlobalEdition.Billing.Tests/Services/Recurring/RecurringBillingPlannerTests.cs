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
/// Unit tests for RecurringBillingPlanner — Stage 1 of the high-volume pipeline.
/// </summary>
public class RecurringBillingPlannerTests
{
    private static readonly DateTime TestDate = new DateTime(2026, 5, 13);

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

    private static SubscriptionBillingState MakeState(long shardKey, string memberId, DateTime nextAttempt)
        => new SubscriptionBillingState
        {
            Id                       = Guid.NewGuid().ToString(),
            MemberId                 = memberId,
            MembershipSubscriptionId = Guid.NewGuid().ToString(),
            RecurringBillingPlanId   = 1,
            BillingAnchorDate        = nextAttempt.AddMonths(-1),
            NextBillingDate          = nextAttempt,
            NextAttemptDate          = nextAttempt,
            Status                   = RecurringBillingStatus.Active,
            ShardKey                 = shardKey,
            CreatedBy                = "test",
            CreationDate             = TestDate,
            LastUpdateDate           = TestDate
        };

    private static MemberProfile MakeProfile(string memberId, string country = "US")
        => new MemberProfile
        {
            // Id (PK) is a separate GUID; MemberId is the human-readable ID that
            // SubscriptionBillingState.MemberId references.
            Id             = Guid.NewGuid().ToString(),
            MemberId       = memberId,
            Country        = country,
            UserId         = Guid.NewGuid(),
            CreatedBy      = "test",
            CreationDate   = TestDate,
            LastUpdateDate = TestDate
        };

    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PlanAsync_WhenNoDueStates_ReturnsBatchesCreatedZero()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var planner  = new RecurringBillingPlanner(db, RouterMock(), DateTimeMock(), Logger());

        // Act
        var result = await planner.PlanAsync(TestDate);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.BatchesCreated.Should().Be(0);
        result.Value!.TotalCases.Should().Be(0);
    }

    [Fact]
    public async Task PlanAsync_WithDueStates_CreatesBatchAndShards()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();

        var memberId = "member-1";
        db.MemberProfiles.Add(MakeProfile(memberId));
        db.SubscriptionBillingStates.Add(MakeState(1, memberId, TestDate));
        db.SubscriptionBillingStates.Add(MakeState(2, memberId, TestDate));
        await db.SaveChangesAsync();

        var planner = new RecurringBillingPlanner(db, RouterMock(), DateTimeMock(), Logger());

        // Act
        var result = await planner.PlanAsync(TestDate);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.BatchesCreated.Should().BeGreaterThan(0);
        result.Value!.TotalCases.Should().Be(2);
        result.Value!.TotalShards.Should().BeGreaterThan(0);

        var batches = await db.RecurringBillingBatches.ToListAsync();
        batches.Should().NotBeEmpty();

        var shards = await db.RecurringBillingBatchShards.ToListAsync();
        shards.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PlanAsync_Idempotent_ReturnsBatchesCreatedZeroOnSecondCall()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();

        var memberId = "member-1";
        db.MemberProfiles.Add(MakeProfile(memberId));
        db.SubscriptionBillingStates.Add(MakeState(1, memberId, TestDate));
        await db.SaveChangesAsync();

        var planner = new RecurringBillingPlanner(db, RouterMock(), DateTimeMock(), Logger());

        // Act — first call creates plan
        var first = await planner.PlanAsync(TestDate);
        // Second call should return existing plan without creating new rows
        var second = await planner.PlanAsync(TestDate);

        // Assert
        first.IsSuccess.Should().BeTrue();
        first.Value!.BatchesCreated.Should().Be(1);

        second.IsSuccess.Should().BeTrue();
        second.Value!.BatchesCreated.Should().Be(0); // idempotency: no new batches

        var batchCount = await db.RecurringBillingBatches.CountAsync();
        batchCount.Should().Be(1); // only one batch created total
    }

    [Fact]
    public async Task PlanAsync_ShardRangesAreDisjoint_NoBillingStateIsCoveredTwice()
    {
        // Arrange — 10 due states across a single processor
        using var db = TestDbContextFactory.Create();

        for (int i = 1; i <= 10; i++)
        {
            var memberId = $"member-{i}";
            db.MemberProfiles.Add(MakeProfile(memberId));
            db.SubscriptionBillingStates.Add(MakeState(i, memberId, TestDate));
        }
        await db.SaveChangesAsync();

        var planner = new RecurringBillingPlanner(db, RouterMock(), DateTimeMock(), Logger());

        // Act
        await planner.PlanAsync(TestDate);

        // Assert — shard ranges must not overlap
        var shards = await db.RecurringBillingBatchShards.ToListAsync();

        // Build sorted pairs and check no overlap
        var sorted = shards.OrderBy(s => s.IdRangeStart).ToList();
        for (int i = 1; i < sorted.Count; i++)
        {
            sorted[i].IdRangeStart.Should().BeGreaterThan(sorted[i - 1].IdRangeEnd,
                because: $"shard {i} range start must be after shard {i - 1} range end");
        }
    }

    [Fact]
    public async Task PlanAsync_StatesNotDueToday_AreNotIncluded()
    {
        // Arrange — one due today, one due tomorrow
        using var db = TestDbContextFactory.Create();

        db.MemberProfiles.Add(MakeProfile("member-due"));
        db.MemberProfiles.Add(MakeProfile("member-future"));
        db.SubscriptionBillingStates.Add(MakeState(1, "member-due", TestDate));
        db.SubscriptionBillingStates.Add(MakeState(2, "member-future", TestDate.AddDays(1)));
        await db.SaveChangesAsync();

        var planner = new RecurringBillingPlanner(db, RouterMock(), DateTimeMock(), Logger());

        // Act
        var result = await planner.PlanAsync(TestDate);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCases.Should().Be(1); // only the due-today state
    }

    [Fact]
    public async Task PlanAsync_WorkerCountClamped_ToConfiguredFloorAndCeiling()
    {
        // Arrange — seed GlobalParameters with low ceiling (max 2 workers)
        using var db = TestDbContextFactory.Create();

        db.GlobalParameters.Add(new GlobalParameter
        {
            Key = "RecurringBilling:MaxConcurrencyPerGateway:NmiSpreedly",
            Value = "2",
            CreatedBy = "test",
            CreationDate = TestDate,
            LastUpdateDate = TestDate
        });
        db.GlobalParameters.Add(new GlobalParameter
        {
            Key = "RecurringBilling:MinWorkersPerGateway:NmiSpreedly",
            Value = "1",
            CreatedBy = "test",
            CreationDate = TestDate,
            LastUpdateDate = TestDate
        });

        // Add 100 due states — formula would normally suggest many workers
        for (int i = 1; i <= 100; i++)
        {
            var mid = $"member-{i}";
            db.MemberProfiles.Add(MakeProfile(mid));
            db.SubscriptionBillingStates.Add(MakeState(i, mid, TestDate));
        }
        await db.SaveChangesAsync();

        var planner = new RecurringBillingPlanner(db, RouterMock(), DateTimeMock(), Logger());

        // Act
        var result = await planner.PlanAsync(TestDate);

        // Assert — batch worker count must be ≤ 2
        result.IsSuccess.Should().BeTrue();
        var batches = await db.RecurringBillingBatches.ToListAsync();
        batches.Should().NotBeEmpty();
        batches.All(b => b.WorkerCount <= 2).Should().BeTrue(
            "worker count must not exceed the configured ceiling of 2");
        batches.All(b => b.WorkerCount >= 1).Should().BeTrue(
            "worker count must meet the configured floor of 1");
    }

    [Fact]
    public async Task PlanAsync_ShardCountMatchesBatchWorkerCount()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();

        for (int i = 1; i <= 6; i++)
        {
            var mid = $"member-{i}";
            db.MemberProfiles.Add(MakeProfile(mid));
            db.SubscriptionBillingStates.Add(MakeState(i, mid, TestDate));
        }
        await db.SaveChangesAsync();

        var planner = new RecurringBillingPlanner(db, RouterMock(), DateTimeMock(), Logger());

        // Act
        await planner.PlanAsync(TestDate);

        // Assert — each batch's shard count should equal its worker count
        var batches = await db.RecurringBillingBatches.Include(b => b.Shards).ToListAsync();
        foreach (var batch in batches)
        {
            batch.Shards.Count.Should().Be(batch.WorkerCount,
                because: $"batch {batch.Id} should have exactly {batch.WorkerCount} shards");
        }
    }
}
