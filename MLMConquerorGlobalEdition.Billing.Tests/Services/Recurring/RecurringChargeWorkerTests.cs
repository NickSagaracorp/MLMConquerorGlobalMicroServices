using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;
using Microsoft.Extensions.Logging;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

/// <summary>
/// Unit tests for RecurringChargeWorker — Stage 2 of the high-volume pipeline.
/// </summary>
public class RecurringChargeWorkerTests
{
    private static readonly DateTime Now = new DateTime(2026, 5, 13, 8, 0, 0);

    private static ILogger<RecurringChargeWorker> Logger()
        => new Mock<ILogger<RecurringChargeWorker>>().Object;

    private static IDateTimeProvider DateTimeMock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(Now);
        return m.Object;
    }

    private static RecurringBillingBatch MakeBatch(string batchId)
        => new RecurringBillingBatch
        {
            Id             = batchId,
            RunDate        = Now.Date,
            Gateway        = CardProcessor.NmiSpreedly,
            WorkerCount    = 1,
            ScheduledStartTime = Now,
            CaseCount      = 2,
            Status         = RecurringBillingBatchStatus.Planned,
            CreatedBy      = "test",
            CreationDate   = Now,
            LastUpdateDate = Now
        };

    private static RecurringBillingBatchShard MakeShard(string shardId, string batchId, long start, long end)
        => new RecurringBillingBatchShard
        {
            Id             = shardId,
            BatchId        = batchId,
            ShardIndex     = 0,
            IdRangeStart   = start,
            IdRangeEnd     = end,
            Status         = RecurringBillingBatchStatus.Planned,
            CasesProcessed = 0,
            CreatedBy      = "test",
            CreationDate   = Now,
            LastUpdateDate = Now
        };

    private static SubscriptionBillingState MakeState(long shardKey, string memberId, string subscriptionId)
        => new SubscriptionBillingState
        {
            Id                       = Guid.NewGuid().ToString(),
            MemberId                 = memberId,
            MembershipSubscriptionId = subscriptionId,
            RecurringBillingPlanId   = 1,
            BillingAnchorDate        = Now.AddMonths(-1),
            NextBillingDate          = Now.Date,
            NextAttemptDate          = Now.Date,
            Status                   = RecurringBillingStatus.Active,
            ShardKey                 = shardKey,
            CreatedBy                = "test",
            CreationDate             = Now,
            LastUpdateDate           = Now
        };

    private static MembershipSubscription MakeSubscription(string subId, string memberId)
        => new MembershipSubscription
        {
            Id                  = subId,
            MemberId            = memberId,
            MembershipLevelId   = 1,
            SubscriptionStatus  = MembershipStatus.Active,
            StartDate           = Now.AddMonths(-1),
            IsAutoRenew         = true,
            DualTeamContribution   = 100,
            EnrollmentContribution = 50,
            PersonalContribution   = 25,
            ChangeReason        = SubscriptionChangeReason.New,
            CreatedBy           = "test",
            CreationDate        = Now,
            LastUpdateDate      = Now
        };

    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessShardAsync_WhenShardNotFound_ReturnsFailure()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var processorMock = new Mock<IRecurringBillingProcessor>();
        var worker = new RecurringChargeWorker(db, processorMock.Object, DateTimeMock(), Logger());

        // Act
        var result = await worker.ProcessShardAsync("nonexistent-shard");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SHARD_NOT_FOUND");
    }

    [Fact]
    public async Task ProcessShardAsync_WhenShardAlreadyDone_ReturnsMappedCount()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var batchId = "batch-1";
        var shardId = "shard-1";

        var batch = MakeBatch(batchId);
        db.RecurringBillingBatches.Add(batch);

        var shard = MakeShard(shardId, batchId, 1, 10);
        shard.Status         = RecurringBillingBatchStatus.Done;
        shard.CasesProcessed = 5;
        db.RecurringBillingBatchShards.Add(shard);
        await db.SaveChangesAsync();

        var processorMock = new Mock<IRecurringBillingProcessor>();
        var worker = new RecurringChargeWorker(db, processorMock.Object, DateTimeMock(), Logger());

        // Act
        var result = await worker.ProcessShardAsync(shardId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ShardId.Should().Be(shardId);
        result.Value!.Skipped.Should().Be(5);
        processorMock.Verify(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never, "processor must not be called for already-done shard");
    }

    [Fact]
    public async Task ProcessShardAsync_OnSuccess_MarksShardDoneAndSetsProcessedCount()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var batchId = "batch-1";
        var shardId = "shard-1";
        var subId   = "sub-1";
        var memberId = "member-1";

        db.RecurringBillingBatches.Add(MakeBatch(batchId));
        db.RecurringBillingBatchShards.Add(MakeShard(shardId, batchId, 1, 5));
        db.SubscriptionBillingStates.Add(MakeState(1, memberId, subId));
        db.MembershipSubscriptions.Add(MakeSubscription(subId, memberId));
        await db.SaveChangesAsync();

        var processorMock = new Mock<IRecurringBillingProcessor>();
        processorMock
            .Setup(p => p.ProcessAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RecurringBillingProcessorResult>.Success(new RecurringBillingProcessorResult
            {
                Outcome          = "Success",
                FundingSource    = "CreditCard",
                PaymentHistoryId = "ph-001"
            }));

        var worker = new RecurringChargeWorker(db, processorMock.Object, DateTimeMock(), Logger());

        // Act
        var result = await worker.ProcessShardAsync(shardId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Succeeded.Should().Be(1);

        var updatedShard = await db.RecurringBillingBatchShards.FirstAsync(s => s.Id == shardId);
        updatedShard.Status.Should().Be(RecurringBillingBatchStatus.Done);
        updatedShard.CasesProcessed.Should().Be(1);
    }

    [Fact]
    public async Task ProcessShardAsync_OnSuccess_EmitsActivatedPointDeltaEvent()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var batchId  = "batch-1";
        var shardId  = "shard-1";
        var subId    = "sub-1";
        var memberId = "member-1";
        var orderId  = "order-1";

        db.RecurringBillingBatches.Add(MakeBatch(batchId));
        db.RecurringBillingBatchShards.Add(MakeShard(shardId, batchId, 1, 5));

        var state = MakeState(1, memberId, subId);
        db.SubscriptionBillingStates.Add(state);
        db.MembershipSubscriptions.Add(MakeSubscription(subId, memberId));
        await db.SaveChangesAsync();

        var processorMock = new Mock<IRecurringBillingProcessor>();
        processorMock
            .Setup(p => p.ProcessAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RecurringBillingProcessorResult>.Success(new RecurringBillingProcessorResult
            {
                Outcome          = "Success",
                FundingSource    = "CreditCard",
                PaymentHistoryId = "ph-001",
                OrderId          = orderId
            }));

        var worker = new RecurringChargeWorker(db, processorMock.Object, DateTimeMock(), Logger());

        // Act
        await worker.ProcessShardAsync(shardId);

        // Assert
        var events = await db.PointDeltaEvents.ToListAsync();
        events.Should().HaveCount(1);
        events[0].EventType.Should().Be(PointDeltaEventType.Activated);
        events[0].DualTeamDelta.Should().Be(100);
        events[0].EnrollmentDelta.Should().Be(50);
        events[0].PersonalDelta.Should().Be(25);
        events[0].Status.Should().Be(PointDeltaEventStatus.Queued);
        events[0].BatchId.Should().Be(batchId);
    }

    [Fact]
    public async Task ProcessShardAsync_OnSuccess_EmitsCommissionTriggerQueueRows()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var batchId  = "batch-1";
        var shardId  = "shard-1";
        var subId    = "sub-1";
        var memberId = "member-1";

        db.RecurringBillingBatches.Add(MakeBatch(batchId));
        db.RecurringBillingBatchShards.Add(MakeShard(shardId, batchId, 1, 5));

        var state = MakeState(1, memberId, subId);
        db.SubscriptionBillingStates.Add(state);
        db.MembershipSubscriptions.Add(MakeSubscription(subId, memberId));
        await db.SaveChangesAsync();

        var processorMock = new Mock<IRecurringBillingProcessor>();
        processorMock
            .Setup(p => p.ProcessAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RecurringBillingProcessorResult>.Success(new RecurringBillingProcessorResult
            {
                Outcome = "Success", FundingSource = "CreditCard", PaymentHistoryId = "ph-001",
                OrderId = "order-1"
            }));

        var worker = new RecurringChargeWorker(db, processorMock.Object, DateTimeMock(), Logger());

        // Act
        await worker.ProcessShardAsync(shardId);

        // Assert — one FSB and one BoostBonus trigger per success
        var triggers = await db.CommissionTriggerQueues.ToListAsync();
        triggers.Should().HaveCount(2);
        triggers.Select(t => t.TriggerType).Should().Contain("FastStartBonus");
        triggers.Select(t => t.TriggerType).Should().Contain("BoostBonus");
        triggers.All(t => t.IsProcessed == false).Should().BeTrue();
    }

    [Fact]
    public async Task ProcessShardAsync_OnlyProcessesStatesInShardKeyRange()
    {
        // Arrange — two states: shardKey 1 (in range) and shardKey 99 (out of range)
        using var db = TestDbContextFactory.Create();
        var batchId  = "batch-1";
        var shardId  = "shard-1";

        db.RecurringBillingBatches.Add(MakeBatch(batchId));
        db.RecurringBillingBatchShards.Add(MakeShard(shardId, batchId, 1, 5)); // range [1,5]

        db.SubscriptionBillingStates.Add(MakeState(1, "member-in",  "sub-in"));
        db.SubscriptionBillingStates.Add(MakeState(99, "member-out", "sub-out"));
        db.MembershipSubscriptions.Add(MakeSubscription("sub-in",  "member-in"));
        db.MembershipSubscriptions.Add(MakeSubscription("sub-out", "member-out"));
        await db.SaveChangesAsync();

        int callCount = 0;
        var processorMock = new Mock<IRecurringBillingProcessor>();
        processorMock
            .Setup(p => p.ProcessAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
            .Callback(() => callCount++)
            .ReturnsAsync(Result<RecurringBillingProcessorResult>.Success(new RecurringBillingProcessorResult
            {
                Outcome = "Success"
            }));

        var worker = new RecurringChargeWorker(db, processorMock.Object, DateTimeMock(), Logger());

        // Act
        await worker.ProcessShardAsync(shardId);

        // Assert — only the in-range state was processed
        callCount.Should().Be(1, "only state with shardKey=1 should be processed");
    }

    [Fact]
    public async Task ProcessShardAsync_Resumable_SkipsAlreadyProcessedStates()
    {
        // Arrange — simulate a prior run that already processed one state
        using var db = TestDbContextFactory.Create();
        var batchId  = "batch-1";
        var shardId  = "shard-1";
        var subId1   = "sub-1";
        var subId2   = "sub-2";

        db.RecurringBillingBatches.Add(MakeBatch(batchId));
        db.RecurringBillingBatchShards.Add(MakeShard(shardId, batchId, 1, 10));

        var state1 = MakeState(1, "member-1", subId1);
        var state2 = MakeState(2, "member-2", subId2);
        db.SubscriptionBillingStates.Add(state1);
        db.SubscriptionBillingStates.Add(state2);

        // state1 was already processed today
        var processedAttempt = new RecurringBillingAttempt
        {
            SubscriptionBillingStateId = state1.Id,
            MemberId   = "member-1",
            ProductId  = "prod-1",
            AttemptIndex = 0,
            AttemptedAt  = Now,
            Amount     = 99m,
            FundingSource = RecurringFundingSource.CreditCard,
            Outcome    = RecurringAttemptOutcome.Success,
            OrderId    = "order-old",
            CreatedBy  = "test",
            CreationDate = Now
        };
        db.RecurringBillingAttempts.Add(processedAttempt);

        db.MembershipSubscriptions.Add(MakeSubscription(subId1, "member-1"));
        db.MembershipSubscriptions.Add(MakeSubscription(subId2, "member-2"));
        await db.SaveChangesAsync();

        // The AuditInterceptor stamps CreationDate with the real wall clock on insert,
        // which would push this attempt out of the worker's mocked "processed today"
        // window and make the test rot over time. Restore the deterministic timestamp
        // via a Modified save (the interceptor only sets CreationDate on Added).
        processedAttempt.CreationDate = Now;
        await db.SaveChangesAsync();

        int callCount = 0;
        var processorMock = new Mock<IRecurringBillingProcessor>();
        processorMock
            .Setup(p => p.ProcessAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
            .Callback(() => callCount++)
            .ReturnsAsync(Result<RecurringBillingProcessorResult>.Success(new RecurringBillingProcessorResult
            {
                Outcome = "Success"
            }));

        var worker = new RecurringChargeWorker(db, processorMock.Object, DateTimeMock(), Logger());

        // Act
        var result = await worker.ProcessShardAsync(shardId);

        // Assert — state1 was skipped (already processed); state2 was processed
        callCount.Should().Be(1, "only un-processed state should be passed to processor");
        result.Value!.Skipped.Should().Be(1);
    }
}
