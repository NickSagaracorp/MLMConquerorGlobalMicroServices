using FluentAssertions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services;

public class GatewaySplitSelectorTests
{
    private static GatewaySplitSelector CreateSelector(AppDbContext db)
    {
        var dateTimeMock = new Mock<IDateTimeProvider>();
        dateTimeMock.Setup(d => d.Now).Returns(DateTime.UtcNow);
        return new GatewaySplitSelector(db, dateTimeMock.Object);
    }

    private static GatewayRoutingRuleSplit Split(CardProcessor proc, decimal weight, int order) =>
        new() { CardProcessor = proc, WeightPercent = weight, SortOrder = order };

    // ── Empty splits ───────────────────────────────────────────────────────

    [Fact]
    public async Task PickAsync_WhenNoSplits_ReturnsFailure()
    {
        using var db = TestDbContextFactory.Create();
        var selector = CreateSelector(db);

        var result = await selector.PickAsync("bucket1", Array.Empty<GatewayRoutingRuleSplit>());

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_SPLITS");
    }

    // ── Single split ───────────────────────────────────────────────────────

    [Fact]
    public async Task PickAsync_WhenSingleSplit_ReturnsItAndIncrementsCounter()
    {
        using var db = TestDbContextFactory.Create();
        var selector = CreateSelector(db);
        var splits = new[] { Split(CardProcessor.NmiSpreedly, 100m, 1) };

        var result = await selector.PickAsync("bucket-single", splits);
        // Flush changes so the counter row is visible to LINQ queries
        await db.SaveChangesAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(CardProcessor.NmiSpreedly);

        var counter = db.GatewayRoutingCounters
            .FirstOrDefault(c => c.RouteBucketKey == "bucket-single");
        counter.Should().NotBeNull();
        counter!.AttemptCount.Should().Be(1);
    }

    // ── 60/40 split over many iterations ──────────────────────────────────
    // The selector reads persisted counters (EF query). Between picks we call
    // SaveChangesAsync so the incremented counter is visible to the next pick.

    [Fact]
    public async Task PickAsync_With60_40Split_DistributesCorrectly()
    {
        using var db = TestDbContextFactory.Create();
        var selector = CreateSelector(db);
        const string bucket = "bucket-6040";
        var splits = new[]
        {
            Split(CardProcessor.NmiSpreedly, 60m, 1),
            Split(CardProcessor.CheckoutUS,  40m, 2)
        };

        int nmi = 0, checkout = 0;
        for (int i = 0; i < 100; i++)
        {
            var result = await selector.PickAsync(bucket, splits);
            // Simulate the outer transaction committing — persists the counter
            await db.SaveChangesAsync();

            result.IsSuccess.Should().BeTrue();
            if (result.Value == CardProcessor.NmiSpreedly) nmi++;
            else checkout++;
        }

        // Deterministic algorithm: should be exact
        nmi.Should().Be(60);
        checkout.Should().Be(40);
    }

    // ── 50/50 split ────────────────────────────────────────────────────────

    [Fact]
    public async Task PickAsync_With50_50Split_DistributesEvenly()
    {
        using var db = TestDbContextFactory.Create();
        var selector = CreateSelector(db);
        const string bucket = "bucket-5050";
        var splits = new[]
        {
            Split(CardProcessor.NmiSpreedly, 50m, 1),
            Split(CardProcessor.CheckoutUS,  50m, 2)
        };

        int nmi = 0, checkout = 0;
        for (int i = 0; i < 100; i++)
        {
            var result = await selector.PickAsync(bucket, splits);
            await db.SaveChangesAsync();

            result.IsSuccess.Should().BeTrue();
            if (result.Value == CardProcessor.NmiSpreedly) nmi++;
            else checkout++;
        }

        nmi.Should().Be(50);
        checkout.Should().Be(50);
    }

    // ── Counter persists across invocations (increments, not resets) ───────

    [Fact]
    public async Task PickAsync_CounterIncrements_AcrossMultipleCalls()
    {
        using var db = TestDbContextFactory.Create();
        var selector = CreateSelector(db);
        const string bucket = "bucket-persist";
        var splits = new[] { Split(CardProcessor.NmiDirect, 100m, 1) };

        for (int i = 1; i <= 5; i++)
        {
            await selector.PickAsync(bucket, splits);
            await db.SaveChangesAsync();
            var counter = db.GatewayRoutingCounters
                .First(c => c.RouteBucketKey == bucket);
            counter.AttemptCount.Should().Be(i);
        }
    }

    // ── Tie-break: lower SortOrder wins ────────────────────────────────────

    [Fact]
    public async Task PickAsync_WhenEqualDeficit_LowerSortOrderWins()
    {
        using var db = TestDbContextFactory.Create();
        var selector = CreateSelector(db);
        // On the very first call all processors have 0 actual → equal deficit.
        // SortOrder=1 should win.
        var splits = new[]
        {
            Split(CardProcessor.CheckoutUS,     50m, 2),
            Split(CardProcessor.NmiSpreedly,    50m, 1)   // lower sort order
        };

        var result = await selector.PickAsync("bucket-tiebreak", splits);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(CardProcessor.NmiSpreedly);
    }

    // ── Buckets are independent (different bucket keys don't share counters) ─

    [Fact]
    public async Task PickAsync_DifferentBuckets_HaveIndependentCounters()
    {
        using var db = TestDbContextFactory.Create();
        var selector = CreateSelector(db);
        var splits = new[] { Split(CardProcessor.NmiSpreedly, 100m, 1) };

        await selector.PickAsync("bucketA", splits);
        await db.SaveChangesAsync();
        await selector.PickAsync("bucketB", splits);
        await db.SaveChangesAsync();

        var counterA = db.GatewayRoutingCounters.First(c => c.RouteBucketKey == "bucketA");
        var counterB = db.GatewayRoutingCounters.First(c => c.RouteBucketKey == "bucketB");

        counterA.AttemptCount.Should().Be(1);
        counterB.AttemptCount.Should().Be(1);
    }
}
