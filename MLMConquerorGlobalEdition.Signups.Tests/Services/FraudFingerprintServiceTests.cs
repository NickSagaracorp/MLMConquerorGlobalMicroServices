using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Services;

/// <summary>
/// FraudFingerprintService records every join-page event and flags duplicates from the same
/// browser fingerprint. The threshold and window are configurable; tests pin them to small
/// values for clarity.
/// </summary>
public class FraudFingerprintServiceTests
{
    private static IConfiguration TightConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FraudGuard:WindowHours"]        = "1",
            ["FraudGuard:DuplicateThreshold"] = "3"
        }).Build();

    /// <summary>
    /// Returns an IDateTimeProvider that mirrors the real clock. We can't use a frozen
    /// timestamp here because AppDbContext's AuditInterceptor unconditionally stamps
    /// CreationDate with <see cref="DateTime.Now"/> on save — so the readback would
    /// disagree with any frozen value and the windowed dup-count would miss every row.
    /// The window is set wide enough (1h) that real-clock drift across a single test
    /// doesn't matter.
    /// </summary>
    private static FraudFingerprintService MakeService(
        Repository.Context.AppDbContext db,
        IConfiguration? config = null)
    {
        var dt = new Mock<IDateTimeProvider>();
        dt.Setup(x => x.Now).Returns(() => DateTime.Now);
        return new FraudFingerprintService(db, dt.Object, config ?? TightConfig(),
            NullLogger<FraudFingerprintService>.Instance);
    }

    [Fact]
    public async Task RecordAsync_FirstEvent_PersistsAndIsNotFlagged()
    {
        await using var db = InMemoryDbHelper.Create();
        var svc = MakeService(db);

        var result = await svc.RecordAsync(
            "visitor-A", SignupRiskFlow.AmbassadorSignup,
            "johndoe", "1.1.1.1", "Mozilla/5.0", null, null, CancellationToken.None);

        result.IsFlagged.Should().BeFalse();
        result.FlagReason.Should().BeNull();

        var rows = await db.SignupRiskFingerprints.AsNoTracking().ToListAsync();
        rows.Should().ContainSingle();
        rows[0].VisitorId.Should().Be("visitor-A");
        rows[0].IpAddress.Should().Be("1.1.1.1");
        rows[0].Flow.Should().Be(SignupRiskFlow.AmbassadorSignup);
        rows[0].IsFlagged.Should().BeFalse();
    }

    [Fact]
    public async Task RecordAsync_WhenSameVisitorReachesThresholdInWindow_FlagsTheRequest()
    {
        await using var db = InMemoryDbHelper.Create();
        var svc = MakeService(db);

        // 1st and 2nd events: under threshold, no flag.
        var first  = await svc.RecordAsync("visitor-X", SignupRiskFlow.AmbassadorSignup, "s", null, null, null, null, CancellationToken.None);
        var second = await svc.RecordAsync("visitor-X", SignupRiskFlow.MemberSignup,    "s", null, null, null, null, CancellationToken.None);
        first.IsFlagged.Should().BeFalse();
        second.IsFlagged.Should().BeFalse();

        // 3rd event: trips the threshold (3 within 1h).
        var third = await svc.RecordAsync("visitor-X", SignupRiskFlow.AmbassadorSignup, "s", null, null, null, null, CancellationToken.None);
        third.IsFlagged.Should().BeTrue();
        third.FlagReason.Should().Be("DUP_VISITOR_3_IN_1H");

        var rowFlagged = await db.SignupRiskFingerprints.AsNoTracking().FirstAsync(r => r.Id == third.EventId);
        rowFlagged.IsFlagged.Should().BeTrue();
        rowFlagged.FlagReason.Should().Be("DUP_VISITOR_3_IN_1H");
    }

    [Fact]
    public async Task RecordAsync_WhenDifferentVisitors_DoesNotFlag()
    {
        await using var db = InMemoryDbHelper.Create();
        var svc = MakeService(db);

        // Three signups but each with its own visitor — no fingerprint overlap, no flag.
        var a = await svc.RecordAsync("visitor-A", SignupRiskFlow.AmbassadorSignup, "s", null, null, null, null, CancellationToken.None);
        var b = await svc.RecordAsync("visitor-B", SignupRiskFlow.AmbassadorSignup, "s", null, null, null, null, CancellationToken.None);
        var c = await svc.RecordAsync("visitor-C", SignupRiskFlow.AmbassadorSignup, "s", null, null, null, null, CancellationToken.None);

        a.IsFlagged.Should().BeFalse();
        b.IsFlagged.Should().BeFalse();
        c.IsFlagged.Should().BeFalse();
    }

    [Fact]
    public async Task RecordAsync_WhenVisitorIdNullOrBlank_GeneratesUnknownIdAndDoesNotFlagOnDups()
    {
        // OSS lib failures (visitorId blank) should not cascade into false-positive flags
        // — we still record the event for IP-level audit but skip the dup count.
        await using var db = InMemoryDbHelper.Create();
        var svc = MakeService(db);

        var first  = await svc.RecordAsync(null, SignupRiskFlow.AmbassadorSignup, "s", "1.1.1.1", null, null, null, CancellationToken.None);
        var second = await svc.RecordAsync("",   SignupRiskFlow.AmbassadorSignup, "s", "1.1.1.1", null, null, null, CancellationToken.None);
        var third  = await svc.RecordAsync(" ",  SignupRiskFlow.AmbassadorSignup, "s", "1.1.1.1", null, null, null, CancellationToken.None);

        first.IsFlagged.Should().BeFalse();
        second.IsFlagged.Should().BeFalse();
        third.IsFlagged.Should().BeFalse();

        // Each blank produces a unique synthetic id — they don't collide so dup count stays at 0.
        var rows = await db.SignupRiskFingerprints.AsNoTracking().ToListAsync();
        rows.Should().HaveCount(3);
        rows.Select(r => r.VisitorId).Distinct().Should().HaveCount(3);
    }

    [Fact]
    public async Task RecordAsync_DefaultThresholdIs3In24Hours()
    {
        // No FraudGuard: keys → defaults apply.
        await using var db = InMemoryDbHelper.Create();
        var emptyConfig = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var svc = MakeService(db, emptyConfig);

        await svc.RecordAsync("v", SignupRiskFlow.AmbassadorSignup, "s", null, null, null, null, CancellationToken.None);
        await svc.RecordAsync("v", SignupRiskFlow.AmbassadorSignup, "s", null, null, null, null, CancellationToken.None);
        var third = await svc.RecordAsync("v", SignupRiskFlow.AmbassadorSignup, "s", null, null, null, null, CancellationToken.None);

        third.IsFlagged.Should().BeTrue();
        third.FlagReason.Should().Be("DUP_VISITOR_3_IN_24H");
    }

    [Fact]
    public async Task RecordAsync_WhenPriorRowsAreCleared_TheyDoNotCountTowardThreshold()
    {
        // Admin clears the prior fingerprints (legitimate user blocked by mistake).
        // After clearing, the visitor must be able to submit at least <threshold> more attempts
        // before the guard trips again.
        await using var db = InMemoryDbHelper.Create();
        var svc = MakeService(db);

        await svc.RecordAsync("visitor-Y", SignupRiskFlow.AmbassadorSignup, "s", null, null, null, null, CancellationToken.None);
        await svc.RecordAsync("visitor-Y", SignupRiskFlow.AmbassadorSignup, "s", null, null, null, null, CancellationToken.None);

        // Admin clears every existing row for this visitor.
        var existing = await db.SignupRiskFingerprints.Where(x => x.VisitorId == "visitor-Y").ToListAsync();
        foreach (var row in existing)
        {
            row.Cleared     = true;
            row.ClearedAt   = DateTime.Now;
            row.ClearedBy   = "admin-uid";
            row.ClearReason = "Support call — confirmed legit";
        }
        await db.SaveChangesAsync();

        // Visitor retries. With cleared rows excluded, count is 0+1 → still under the threshold of 3.
        var third = await svc.RecordAsync("visitor-Y", SignupRiskFlow.AmbassadorSignup, "s", null, null, null, null, CancellationToken.None);
        third.IsFlagged.Should().BeFalse();

        // Two more attempts to confirm the counter genuinely restarted.
        var fourth = await svc.RecordAsync("visitor-Y", SignupRiskFlow.AmbassadorSignup, "s", null, null, null, null, CancellationToken.None);
        fourth.IsFlagged.Should().BeFalse();

        var fifth = await svc.RecordAsync("visitor-Y", SignupRiskFlow.AmbassadorSignup, "s", null, null, null, null, CancellationToken.None);
        fifth.IsFlagged.Should().BeTrue();
        fifth.FlagReason.Should().Be("DUP_VISITOR_3_IN_1H");
    }

    [Fact]
    public async Task RecordAsync_TruncatesOversizedFields()
    {
        await using var db = InMemoryDbHelper.Create();
        var svc = MakeService(db);

        var longUa = new string('x', 2000);
        var longIp = new string('1', 80);

        var result = await svc.RecordAsync(
            "visitor-Z", SignupRiskFlow.AmbassadorSignup,
            new string('s', 200), longIp, longUa,
            null, null, CancellationToken.None);

        result.IsFlagged.Should().BeFalse();
        var row = await db.SignupRiskFingerprints.AsNoTracking().FirstAsync(r => r.Id == result.EventId);
        row.UserAgent!.Length.Should().Be(500);
        row.IpAddress!.Length.Should().Be(45);
        row.SponsorReplicateSite!.Length.Should().Be(100);
    }
}
