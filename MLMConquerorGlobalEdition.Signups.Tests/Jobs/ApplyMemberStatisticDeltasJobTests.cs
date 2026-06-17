using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.Repository.Jobs;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Jobs;

public class ApplyMemberStatisticDeltasJobTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc);

    private static IDateTimeProvider DateTimeMock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m.Object;
    }

    private static ApplyMemberStatisticDeltasJob CreateJob(MLMConquerorGlobalEdition.Repository.Context.AppDbContext db)
        => new(db, DateTimeMock(), NullLogger<ApplyMemberStatisticDeltasJob>.Instance);

    private static MemberStatisticDelta BuildDelta(
        string memberId,
        int enrollmentPoints = 10,
        int teamSize = 1,
        int qsm = 0,
        string sourceMemberId = "AMB-SOURCE",
        bool isApplied = false) => new()
    {
        MemberId                       = memberId,
        EnrollmentPointsDelta          = enrollmentPoints,
        EnrollmentTeamSizeDelta        = teamSize,
        QualifiedSponsoredMembersDelta = qsm,
        SourceMemberId                 = sourceMemberId,
        IsApplied                      = isApplied,
        CreatedBy                      = "test",
        CreationDate                   = FixedNow
    };

    [Fact]
    public async Task ExecuteAsync_WithNoDeltas_IsNoOp()
    {
        await using var db = InMemoryDbHelper.Create();
        var job = CreateJob(db);

        await job.ExecuteAsync(CancellationToken.None);

        var stats = await db.MemberStatistics.ToListAsync();
        stats.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_GroupsByMemberAndSumsDeltas()
    {
        await using var db = InMemoryDbHelper.Create();

        // Three deltas for the same upline — should produce ONE MemberStatistics row
        // with summed values.
        await db.MemberStatisticDeltas.AddRangeAsync(
            BuildDelta("AMB-UPLINE", enrollmentPoints: 10, teamSize: 1, qsm: 1, sourceMemberId: "AMB-S1"),
            BuildDelta("AMB-UPLINE", enrollmentPoints: 25, teamSize: 1, qsm: 0, sourceMemberId: "AMB-S2"),
            BuildDelta("AMB-UPLINE", enrollmentPoints:  5, teamSize: 1, qsm: 0, sourceMemberId: "AMB-S3"));
        await db.SaveChangesAsync();

        await CreateJob(db).ExecuteAsync(CancellationToken.None);

        var stats = await db.MemberStatistics.FirstAsync(s => s.MemberId == "AMB-UPLINE");
        stats.EnrollmentPoints.Should().Be(40);
        stats.EnrollmentTeamSize.Should().Be(3);
        stats.QualifiedSponsoredMembers.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_MarksClaimedDeltasAsApplied()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberStatisticDeltas.AddRangeAsync(
            BuildDelta("AMB-X"),
            BuildDelta("AMB-Y"));
        await db.SaveChangesAsync();

        await CreateJob(db).ExecuteAsync(CancellationToken.None);

        var deltas = await db.MemberStatisticDeltas.ToListAsync();
        deltas.Should().AllSatisfy(d =>
        {
            d.IsApplied.Should().BeTrue();
            d.AppliedAt.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotReapplyAlreadyAppliedDeltas()
    {
        await using var db = InMemoryDbHelper.Create();
        // Seed an existing MemberStatistics row with a known starting value.
        await db.MemberStatistics.AddAsync(new MemberStatisticEntity
        {
            MemberId         = "AMB-X",
            EnrollmentPoints = 100,
            CreatedBy        = "seed",
            CreationDate     = FixedNow
        });
        // Pre-applied delta — must NOT be re-rolled.
        await db.MemberStatisticDeltas.AddAsync(
            BuildDelta("AMB-X", enrollmentPoints: 50, isApplied: true));
        await db.SaveChangesAsync();

        await CreateJob(db).ExecuteAsync(CancellationToken.None);

        var stats = await db.MemberStatistics.FirstAsync(s => s.MemberId == "AMB-X");
        stats.EnrollmentPoints.Should().Be(100,
            "already-applied deltas must be skipped — re-applying would double-count.");
    }

    [Fact]
    public async Task ExecuteAsync_IsIdempotent_WhenRunTwice()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberStatisticDeltas.AddAsync(
            BuildDelta("AMB-IDEM", enrollmentPoints: 30, teamSize: 2, qsm: 1));
        await db.SaveChangesAsync();

        var job = CreateJob(db);
        await job.ExecuteAsync(CancellationToken.None);
        await job.ExecuteAsync(CancellationToken.None);   // second pass should be no-op

        var stats = await db.MemberStatistics.FirstAsync(s => s.MemberId == "AMB-IDEM");
        stats.EnrollmentPoints.Should().Be(30);
        stats.EnrollmentTeamSize.Should().Be(2);
        stats.QualifiedSponsoredMembers.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_CreatesNewStatsRow_WhenAncestorHadNone()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberStatisticDeltas.AddAsync(
            BuildDelta("AMB-FRESH", enrollmentPoints: 15, teamSize: 1, qsm: 1));
        await db.SaveChangesAsync();

        await CreateJob(db).ExecuteAsync(CancellationToken.None);

        var stats = await db.MemberStatistics.FirstOrDefaultAsync(s => s.MemberId == "AMB-FRESH");
        stats.Should().NotBeNull();
        stats!.EnrollmentPoints.Should().Be(15);
        stats.EnrollmentTeamSize.Should().Be(1);
        stats.QualifiedSponsoredMembers.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_AccumulatesOntoExistingStatsRow()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberStatistics.AddAsync(new MemberStatisticEntity
        {
            MemberId                  = "AMB-EXISTING",
            EnrollmentPoints          = 100,
            EnrollmentTeamSize        = 5,
            QualifiedSponsoredMembers = 2,
            CreatedBy                 = "seed",
            CreationDate              = FixedNow
        });
        await db.MemberStatisticDeltas.AddAsync(
            BuildDelta("AMB-EXISTING", enrollmentPoints: 20, teamSize: 1, qsm: 1));
        await db.SaveChangesAsync();

        await CreateJob(db).ExecuteAsync(CancellationToken.None);

        var stats = await db.MemberStatistics.FirstAsync(s => s.MemberId == "AMB-EXISTING");
        stats.EnrollmentPoints.Should().Be(120);
        stats.EnrollmentTeamSize.Should().Be(6);
        stats.QualifiedSponsoredMembers.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_EnqueuesRankEvaluationQueueEntryPerTouchedMember()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberStatisticDeltas.AddRangeAsync(
            BuildDelta("AMB-A"),
            BuildDelta("AMB-A"),     // duplicate upline — one queue row
            BuildDelta("AMB-B"));    // different upline — another queue row
        await db.SaveChangesAsync();

        await CreateJob(db).ExecuteAsync(CancellationToken.None);

        var queued = await db.RankEvaluationQueue
            .Where(q => !q.IsProcessed && q.TriggerEvent == RankEvaluationTrigger.Enrollment)
            .ToListAsync();

        queued.Should().HaveCount(2,
            "one re-evaluation per distinct touched member — not one per delta");
        queued.Select(q => q.EvaluateMemberId).Should().BeEquivalentTo(["AMB-A", "AMB-B"]);
        queued.Should().AllSatisfy(q =>
        {
            q.CreatedBy.Should().Be("delta-apply");
            q.IsProcessed.Should().BeFalse();
        });
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotDuplicateRankEvaluation_WhenOneAlreadyQueued()
    {
        await using var db = InMemoryDbHelper.Create();
        // Pre-existing unprocessed rank evaluation for AMB-A.
        await db.RankEvaluationQueue.AddAsync(new RankEvaluationQueue
        {
            TriggerMemberId  = "AMB-CHILD",
            EvaluateMemberId = "AMB-A",
            TriggerEvent     = RankEvaluationTrigger.Enrollment,
            TriggerDate      = FixedNow.AddMinutes(-3),
            CreatedBy        = "signup",
            CreationDate     = FixedNow.AddMinutes(-3)
        });
        await db.MemberStatisticDeltas.AddAsync(BuildDelta("AMB-A"));
        await db.SaveChangesAsync();

        await CreateJob(db).ExecuteAsync(CancellationToken.None);

        var queued = await db.RankEvaluationQueue
            .Where(q => q.EvaluateMemberId == "AMB-A" && !q.IsProcessed)
            .ToListAsync();

        queued.Should().HaveCount(1,
            "dedup against existing unprocessed entries prevents queue pile-up.");
    }
}
