using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

/// <summary>
/// Unit tests for UplineAggregator — Stage 3 of the high-volume pipeline.
/// </summary>
public class UplineAggregatorTests
{
    private static readonly DateTime Now = new DateTime(2026, 5, 13, 9, 0, 0);

    private static ILogger<UplineAggregator> Logger()
        => new Mock<ILogger<UplineAggregator>>().Object;

    private static IDateTimeProvider DateTimeMock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(Now);
        return m.Object;
    }

    private static PointDeltaEvent MakeQueuedEvent(
        string batchId, string memberId, string orderId,
        int dtDelta, int enrDelta, int personalDelta,
        PointDeltaEventType type = PointDeltaEventType.Activated)
        => new PointDeltaEvent
        {
            BatchId         = batchId,
            OrderId         = orderId,
            MemberId        = memberId,
            EventType       = type,
            DualTeamDelta   = dtDelta,
            EnrollmentDelta = enrDelta,
            PersonalDelta   = personalDelta,
            OccurredAt      = Now,
            Status          = PointDeltaEventStatus.Queued,
            CreatedBy       = "test",
            CreationDate    = Now
        };

    private static MemberStatisticEntity MakeStat(string memberId, int dt = 0, int enr = 0, int personal = 0)
        => new MemberStatisticEntity
        {
            MemberId         = memberId,
            DualTeamPoints   = dt,
            EnrollmentPoints = enr,
            PersonalPoints   = personal,
            CreatedBy        = "test",
            CreationDate     = Now
        };

    private static GenealogyEntity MakeGenealogy(string memberId, string hierarchyPath)
        => new GenealogyEntity
        {
            Id             = Guid.NewGuid().ToString(),
            MemberId       = memberId,
            HierarchyPath  = hierarchyPath,
            Level          = hierarchyPath.Trim('/').Split('/').Length,
            CreatedBy      = "test",
            CreationDate   = Now,
            LastUpdateDate = Now
        };

    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AggregateAsync_WhenNoQueuedEvents_ReturnsZeroApplied()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var aggregator = new UplineAggregator(db, DateTimeMock(), Logger());

        // Act
        var result = await aggregator.AggregateAsync("batch-1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.EventsApplied.Should().Be(0);
        result.Value!.UplineMembersUpdated.Should().Be(0);
    }

    [Fact]
    public async Task AggregateAsync_AppliesDeltaToUplineMembers()
    {
        // Arrange — downline member-3 under member-2 under member-1
        // HierarchyPath for member-3: "/member-1/member-2/member-3/"
        using var db = TestDbContextFactory.Create();
        const string batchId = "batch-1";

        db.GenealogyTree.Add(MakeGenealogy("member-3", "/member-1/member-2/member-3/"));
        db.MemberStatistics.Add(MakeStat("member-1", dt: 0, enr: 0));
        db.MemberStatistics.Add(MakeStat("member-2", dt: 0, enr: 0));
        db.MemberStatistics.Add(MakeStat("member-3", personal: 0));

        db.PointDeltaEvents.Add(MakeQueuedEvent(batchId, "member-3", "order-1",
            dtDelta: 100, enrDelta: 50, personalDelta: 25));
        await db.SaveChangesAsync();

        var aggregator = new UplineAggregator(db, DateTimeMock(), Logger());

        // Act
        var result = await aggregator.AggregateAsync(batchId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.EventsApplied.Should().Be(1);

        var stat1 = await db.MemberStatistics.FirstAsync(s => s.MemberId == "member-1");
        stat1.DualTeamPoints.Should().Be(100);
        stat1.EnrollmentPoints.Should().Be(50);

        var stat2 = await db.MemberStatistics.FirstAsync(s => s.MemberId == "member-2");
        stat2.DualTeamPoints.Should().Be(100);
        stat2.EnrollmentPoints.Should().Be(50);

        var stat3 = await db.MemberStatistics.FirstAsync(s => s.MemberId == "member-3");
        stat3.PersonalPoints.Should().Be(25);
        // member-3's DualTeam/Enrollment should NOT be changed (personalDelta goes to self, dtDelta/enrDelta go to upline)
    }

    [Fact]
    public async Task AggregateAsync_ReducesMultipleEventsToOneUpdatePerUpline()
    {
        // Arrange — two downline members both under member-1
        using var db = TestDbContextFactory.Create();
        const string batchId = "batch-1";

        db.GenealogyTree.Add(MakeGenealogy("member-2", "/member-1/member-2/"));
        db.GenealogyTree.Add(MakeGenealogy("member-3", "/member-1/member-3/"));
        db.MemberStatistics.Add(MakeStat("member-1", dt: 0, enr: 0));
        db.MemberStatistics.Add(MakeStat("member-2", personal: 0));
        db.MemberStatistics.Add(MakeStat("member-3", personal: 0));

        db.PointDeltaEvents.Add(MakeQueuedEvent(batchId, "member-2", "order-1", dtDelta: 100, enrDelta: 50, personalDelta: 0));
        db.PointDeltaEvents.Add(MakeQueuedEvent(batchId, "member-3", "order-2", dtDelta: 200, enrDelta: 100, personalDelta: 0));
        await db.SaveChangesAsync();

        var aggregator = new UplineAggregator(db, DateTimeMock(), Logger());

        // Act
        var result = await aggregator.AggregateAsync(batchId);

        // Assert — member-1 gets both deltas summed in one UPDATE
        result.IsSuccess.Should().BeTrue();
        result.Value!.EventsApplied.Should().Be(2);

        var stat1 = await db.MemberStatistics.FirstAsync(s => s.MemberId == "member-1");
        stat1.DualTeamPoints.Should().Be(300);  // 100 + 200
        stat1.EnrollmentPoints.Should().Be(150); // 50 + 100
    }

    [Fact]
    public async Task AggregateAsync_CommutativeNetDelta_ActivateAndDeactivateEqualsZero()
    {
        // Arrange — same member activated (+50) and then deactivated (-50)
        using var db = TestDbContextFactory.Create();
        const string batchId = "batch-1";

        db.GenealogyTree.Add(MakeGenealogy("member-2", "/member-1/member-2/"));
        db.MemberStatistics.Add(MakeStat("member-1", dt: 200)); // starting value

        db.PointDeltaEvents.Add(MakeQueuedEvent(batchId, "member-2", "order-1",
            dtDelta: 50, enrDelta: 0, personalDelta: 0, type: PointDeltaEventType.Activated));
        db.PointDeltaEvents.Add(MakeQueuedEvent(batchId, "member-2", "order-2",
            dtDelta: -50, enrDelta: 0, personalDelta: 0, type: PointDeltaEventType.Deactivated));
        await db.SaveChangesAsync();

        var aggregator = new UplineAggregator(db, DateTimeMock(), Logger());

        // Act
        await aggregator.AggregateAsync(batchId);

        // Assert — net delta is 0; member-1 remains at 200
        var stat1 = await db.MemberStatistics.FirstAsync(s => s.MemberId == "member-1");
        stat1.DualTeamPoints.Should().Be(200);
    }

    [Fact]
    public async Task AggregateAsync_MarkEventsApplied_AfterProcessing()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        const string batchId = "batch-1";

        db.GenealogyTree.Add(MakeGenealogy("member-2", "/member-1/member-2/"));
        db.MemberStatistics.Add(MakeStat("member-1"));

        db.PointDeltaEvents.Add(MakeQueuedEvent(batchId, "member-2", "order-1",
            dtDelta: 50, enrDelta: 25, personalDelta: 0));
        await db.SaveChangesAsync();

        var aggregator = new UplineAggregator(db, DateTimeMock(), Logger());

        // Act
        await aggregator.AggregateAsync(batchId);

        // Assert
        var events = await db.PointDeltaEvents.ToListAsync();
        events.Should().AllSatisfy(e => e.Status.Should().Be(PointDeltaEventStatus.Applied));
        events.Should().AllSatisfy(e => e.AppliedAt.Should().NotBeNull());
    }

    [Fact]
    public async Task AggregateAsync_Idempotent_DoesNotDoubleApplyOnRetry()
    {
        // Arrange — events already Applied from a prior run
        using var db = TestDbContextFactory.Create();
        const string batchId = "batch-1";

        db.GenealogyTree.Add(MakeGenealogy("member-2", "/member-1/member-2/"));
        db.MemberStatistics.Add(MakeStat("member-1", dt: 100)); // already updated

        // Simulate previously applied event — status = Applied, not Queued
        var alreadyApplied = MakeQueuedEvent(batchId, "member-2", "order-1", dtDelta: 100, enrDelta: 50, personalDelta: 0);
        alreadyApplied.Status   = PointDeltaEventStatus.Applied;
        alreadyApplied.AppliedAt = Now.AddMinutes(-30);
        db.PointDeltaEvents.Add(alreadyApplied);
        await db.SaveChangesAsync();

        var aggregator = new UplineAggregator(db, DateTimeMock(), Logger());

        // Act — re-running should find no Queued events
        var result = await aggregator.AggregateAsync(batchId);

        // Assert — nothing applied again; stat remains at 100
        result.IsSuccess.Should().BeTrue();
        result.Value!.EventsApplied.Should().Be(0);

        var stat1 = await db.MemberStatistics.FirstAsync(s => s.MemberId == "member-1");
        stat1.DualTeamPoints.Should().Be(100); // unchanged
    }
}
