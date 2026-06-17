using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Security;
using MLMConquerorGlobalEdition.AdminAPI.Features.Security.GetFlaggedSignups;
using MLMConquerorGlobalEdition.AdminAPI.Features.Security.UnblockFingerprint;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Security;

public class FlaggedSignupsHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<ICurrentUserService> AdminUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("admin-007");
        return m;
    }

    private static Mock<IDateTimeProvider> Clock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static SignupRiskFingerprint MakeRow(
        string visitorId, string? ip = null, bool flagged = false, bool cleared = false,
        DateTime? when = null, string? sponsor = null)
        => new()
        {
            VisitorId            = visitorId,
            Flow                 = SignupRiskFlow.AmbassadorSignup,
            IpAddress            = ip,
            SponsorReplicateSite = sponsor,
            IsFlagged            = flagged,
            Cleared              = cleared,
            CreationDate         = when ?? DateTime.UtcNow,
            CreatedBy            = "anon"
        };

    // ── GetFlaggedSignupsHandler ──────────────────────────────────────────────

    [Fact]
    public async Task GetFlaggedSignups_DefaultFilters_ReturnsRecentRowsExcludingCleared()
    {
        await using var db = InMemoryDbHelper.Create();
        db.SignupRiskFingerprints.AddRange(
            MakeRow("v-1", flagged: true,  cleared: false),
            MakeRow("v-2", flagged: false, cleared: false),
            MakeRow("v-3", flagged: true,  cleared: true)); // hidden by default
        await db.SaveChangesAsync();

        var handler = new GetFlaggedSignupsHandler(db);
        var result  = await handler.Handle(new GetFlaggedSignupsQuery(
            null, null, null, null, null, OnlyFlagged: false, IncludeCleared: false,
            Page: 1, PageSize: 25), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items.Select(x => x.VisitorId).Should().BeEquivalentTo(new[] { "v-1", "v-2" });
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetFlaggedSignups_OnlyFlagged_FiltersToFlaggedOnly()
    {
        await using var db = InMemoryDbHelper.Create();
        db.SignupRiskFingerprints.AddRange(
            MakeRow("v-1", flagged: true),
            MakeRow("v-2", flagged: false));
        await db.SaveChangesAsync();

        var handler = new GetFlaggedSignupsHandler(db);
        var result  = await handler.Handle(new GetFlaggedSignupsQuery(
            null, null, null, null, null, OnlyFlagged: true, IncludeCleared: false,
            Page: 1, PageSize: 25), CancellationToken.None);

        result.Value!.Items.Should().ContainSingle().Which.VisitorId.Should().Be("v-1");
    }

    [Fact]
    public async Task GetFlaggedSignups_IncludeCleared_ReturnsClearedRowsToo()
    {
        await using var db = InMemoryDbHelper.Create();
        db.SignupRiskFingerprints.AddRange(
            MakeRow("v-1", cleared: false),
            MakeRow("v-2", cleared: true));
        await db.SaveChangesAsync();

        var handler = new GetFlaggedSignupsHandler(db);
        var result  = await handler.Handle(new GetFlaggedSignupsQuery(
            null, null, null, null, null, OnlyFlagged: false, IncludeCleared: true,
            Page: 1, PageSize: 25), CancellationToken.None);

        result.Value!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetFlaggedSignups_FilterByVisitorIdAndIp_NarrowsResults()
    {
        await using var db = InMemoryDbHelper.Create();
        db.SignupRiskFingerprints.AddRange(
            MakeRow("v-1", ip: "1.1.1.1"),
            MakeRow("v-1", ip: "2.2.2.2"),
            MakeRow("v-2", ip: "1.1.1.1"));
        await db.SaveChangesAsync();

        var handler = new GetFlaggedSignupsHandler(db);
        var result  = await handler.Handle(new GetFlaggedSignupsQuery(
            VisitorId: "v-1", IpAddress: "1.1.1.1",
            null, null, null, OnlyFlagged: false, IncludeCleared: false,
            Page: 1, PageSize: 25), CancellationToken.None);

        result.Value!.Items.Should().ContainSingle();
        result.Value.Items.First().VisitorId.Should().Be("v-1");
        result.Value.Items.First().IpAddress.Should().Be("1.1.1.1");
    }

    /// <summary>
    /// AuditInterceptor stamps CreationDate = now on Added entities (AuditChangesLongKey path),
    /// so the value we pass through MakeRow gets overridden by SaveChanges. To test date-sensitive
    /// behavior we save once (interceptor runs), then patch CreationDate and save again — the
    /// second save is Modified state and the interceptor leaves CreationDate alone.
    /// </summary>
    private static async Task PatchCreationDateAsync(
        Repository.Context.AppDbContext db,
        IEnumerable<(string VisitorId, DateTime When)> overrides)
    {
        var map = overrides.ToDictionary(o => o.VisitorId, o => o.When);
        var rows = await db.SignupRiskFingerprints.Where(r => map.Keys.Contains(r.VisitorId)).ToListAsync();
        foreach (var row in rows) row.CreationDate = map[row.VisitorId];
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetFlaggedSignups_FilterBySponsorAndDateWindow_Works()
    {
        await using var db = InMemoryDbHelper.Create();
        db.SignupRiskFingerprints.AddRange(
            MakeRow("v-1", sponsor: "alice"),
            MakeRow("v-2", sponsor: "bob"),
            MakeRow("v-3", sponsor: "alice"));
        await db.SaveChangesAsync();
        await PatchCreationDateAsync(db, new[]
        {
            ("v-1", new DateTime(2026, 5, 20)),
            ("v-2", new DateTime(2026, 5, 26)),
            ("v-3", new DateTime(2026, 5, 26))
        });

        var handler = new GetFlaggedSignupsHandler(db);
        var result  = await handler.Handle(new GetFlaggedSignupsQuery(
            null, null, SponsorReplicateSite: "alice",
            From: new DateTime(2026, 5, 25), To: new DateTime(2026, 5, 27),
            OnlyFlagged: false, IncludeCleared: false,
            Page: 1, PageSize: 25), CancellationToken.None);

        result.Value!.Items.Should().ContainSingle().Which.VisitorId.Should().Be("v-3");
    }

    [Fact]
    public async Task GetFlaggedSignups_OrdersByCreationDateDescending()
    {
        await using var db = InMemoryDbHelper.Create();
        db.SignupRiskFingerprints.AddRange(
            MakeRow("oldest"),
            MakeRow("newest"),
            MakeRow("middle"));
        await db.SaveChangesAsync();
        await PatchCreationDateAsync(db, new[]
        {
            ("oldest", new DateTime(2026, 1, 1)),
            ("newest", new DateTime(2026, 5, 1)),
            ("middle", new DateTime(2026, 3, 1))
        });

        var handler = new GetFlaggedSignupsHandler(db);
        var result  = await handler.Handle(new GetFlaggedSignupsQuery(
            null, null, null, null, null, false, false, 1, 25), CancellationToken.None);

        result.Value!.Items.Select(x => x.VisitorId).Should().Equal("newest", "middle", "oldest");
    }

    [Fact]
    public async Task GetFlaggedSignups_PaginatesCorrectly()
    {
        await using var db = InMemoryDbHelper.Create();
        for (int i = 1; i <= 30; i++)
            db.SignupRiskFingerprints.Add(MakeRow($"v-{i:D2}", when: new DateTime(2026, 5, i % 28 + 1)));
        await db.SaveChangesAsync();

        var handler = new GetFlaggedSignupsHandler(db);
        var page2   = await handler.Handle(new GetFlaggedSignupsQuery(
            null, null, null, null, null, false, false, Page: 2, PageSize: 10), CancellationToken.None);

        page2.Value!.Items.Should().HaveCount(10);
        page2.Value.TotalCount.Should().Be(30);
        page2.Value.Page.Should().Be(2);
        page2.Value.PageSize.Should().Be(10);
    }

    // ── UnblockFingerprintHandler ─────────────────────────────────────────────

    [Fact]
    public async Task Unblock_ByVisitorId_MarksAllMatchingRowsCleared()
    {
        await using var db = InMemoryDbHelper.Create();
        db.SignupRiskFingerprints.AddRange(
            MakeRow("blocked", flagged: true),
            MakeRow("blocked", flagged: true),
            MakeRow("other",   flagged: true));
        await db.SaveChangesAsync();

        var handler = new UnblockFingerprintHandler(db, Clock().Object, AdminUser().Object);
        var result  = await handler.Handle(new UnblockFingerprintCommand(new UnblockFingerprintRequest
        {
            VisitorId = "blocked",
            Reason    = "Support call — confirmed legit"
        }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);

        var cleared = await db.SignupRiskFingerprints.Where(x => x.VisitorId == "blocked").ToListAsync();
        cleared.Should().AllSatisfy(r =>
        {
            r.Cleared.Should().BeTrue();
            r.ClearedAt.Should().Be(FixedNow);
            r.ClearedBy.Should().Be("admin-007");
            r.ClearReason.Should().Be("Support call — confirmed legit");
        });

        // The unrelated visitor is untouched.
        var other = await db.SignupRiskFingerprints.FirstAsync(x => x.VisitorId == "other");
        other.Cleared.Should().BeFalse();
    }

    [Fact]
    public async Task Unblock_ByIpAddress_MarksAllMatchingRowsCleared()
    {
        await using var db = InMemoryDbHelper.Create();
        db.SignupRiskFingerprints.AddRange(
            MakeRow("v-1", ip: "9.9.9.9"),
            MakeRow("v-2", ip: "9.9.9.9"),
            MakeRow("v-3", ip: "1.1.1.1"));
        await db.SaveChangesAsync();

        var handler = new UnblockFingerprintHandler(db, Clock().Object, AdminUser().Object);
        var result  = await handler.Handle(new UnblockFingerprintCommand(new UnblockFingerprintRequest
        {
            IpAddress = "9.9.9.9",
            Reason    = "Office NAT — many users behind one IP"
        }), CancellationToken.None);

        result.Value.Should().Be(2);
        var cleared = await db.SignupRiskFingerprints.Where(x => x.IpAddress == "9.9.9.9").ToListAsync();
        cleared.Should().AllSatisfy(r => r.Cleared.Should().BeTrue());
    }

    [Fact]
    public async Task Unblock_AlreadyClearedRows_AreNotTouchedAgain()
    {
        var earlier = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await using var db = InMemoryDbHelper.Create();
        db.SignupRiskFingerprints.Add(new SignupRiskFingerprint
        {
            VisitorId    = "v-1",
            Flow         = SignupRiskFlow.AmbassadorSignup,
            CreationDate = earlier,
            CreatedBy    = "anon",
            Cleared      = true,
            ClearedAt    = earlier,
            ClearedBy    = "previous-admin",
            ClearReason  = "older clear"
        });
        await db.SaveChangesAsync();

        var handler = new UnblockFingerprintHandler(db, Clock().Object, AdminUser().Object);
        var result  = await handler.Handle(new UnblockFingerprintCommand(new UnblockFingerprintRequest
        {
            VisitorId = "v-1",
            Reason    = "Trying again"
        }), CancellationToken.None);

        result.Value.Should().Be(0);

        var row = await db.SignupRiskFingerprints.SingleAsync(x => x.VisitorId == "v-1");
        row.ClearedBy.Should().Be("previous-admin"); // untouched
        row.ClearReason.Should().Be("older clear");
        row.ClearedAt.Should().Be(earlier);
    }

    [Fact]
    public async Task Unblock_NoMatchingRows_ReturnsZeroWithSuccess()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UnblockFingerprintHandler(db, Clock().Object, AdminUser().Object);

        var result = await handler.Handle(new UnblockFingerprintCommand(new UnblockFingerprintRequest
        {
            VisitorId = "ghost",
            Reason    = "checking"
        }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
    }
}
