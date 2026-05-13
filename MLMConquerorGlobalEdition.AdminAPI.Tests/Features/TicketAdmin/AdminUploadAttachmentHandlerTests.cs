using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminUploadAttachment;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.TicketAdmin;

/// <summary>
/// Covers the admin/staff ticket-attachment upload handler. Unlike the BizCenter side,
/// admins can attach to *any* ticket, regardless of who owns it — that's the asymmetry
/// these tests pin down.
/// </summary>
public class AdminUploadAttachmentHandlerTests : IDisposable
{
    private static readonly DateTime FixedNow = new(2026, 5, 6, 16, 0, 0, DateTimeKind.Utc);

    private readonly string _tempRoot;

    public AdminUploadAttachmentHandlerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"mlm-admin-attach-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true);
    }

    private static Mock<ICurrentUserService> CurrentUser(string userId = "admin-001")
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns(userId);
        return m;
    }

    private static Mock<IDateTimeProvider> DateTimeProvider()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private Mock<IWebHostEnvironment> WebEnv()
    {
        var m = new Mock<IWebHostEnvironment>();
        m.Setup(e => e.WebRootPath).Returns(_tempRoot);
        return m;
    }

    private static Mock<IHttpContextAccessor> NoHttpContext()
    {
        var m = new Mock<IHttpContextAccessor>();
        m.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        return m;
    }

    private static Ticket BuildTicket(string memberId = "AMB-001") => new()
    {
        MemberId       = memberId,
        CategoryId     = 1,
        Subject        = "Sample",
        Body           = "Body",
        Status         = TicketStatus.Open,
        Priority       = TicketPriority.Normal,
        CreationDate   = FixedNow.AddDays(-1),
        LastUpdateDate = FixedNow.AddDays(-1),
        CreatedBy      = "seed"
    };

    [Fact]
    public async Task Handle_WhenTicketNotFound_ReturnsTicketNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new AdminUploadAttachmentHandler(
            db, CurrentUser().Object, DateTimeProvider().Object, WebEnv().Object, NoHttpContext().Object);

        var result = await handler.Handle(new AdminUploadAttachmentCommand(
            TicketId:         "missing-id",
            OriginalFileName: "note.txt",
            ContentType:      "text/plain",
            FileSizeBytes:    1,
            Content:          new byte[] { (byte)'x' }), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TICKET_NOT_FOUND");
        db.TicketAttachments.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTicketBelongsToAnotherMember_AdminCanStillAttach()
    {
        // Asymmetry vs. BizCenter: an admin uploading to *any* member's ticket succeeds.
        await using var db = InMemoryDbHelper.Create();
        var ticket = BuildTicket(memberId: "owner-amb-555");
        await db.Tickets.AddAsync(ticket);
        await db.SaveChangesAsync();

        var handler = new AdminUploadAttachmentHandler(
            db, CurrentUser().Object, DateTimeProvider().Object, WebEnv().Object, NoHttpContext().Object);

        var bytes = new byte[] { 1, 2, 3 };
        var result = await handler.Handle(new AdminUploadAttachmentCommand(
            TicketId:         ticket.Id,
            OriginalFileName: "evidence.png",
            ContentType:      "image/png",
            FileSizeBytes:    bytes.Length,
            Content:          bytes), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FileName.Should().Be("evidence.png");
        result.Value.ContentType.Should().Be("image/png");
        result.Value.UploadedBy.Should().Be("admin-001");
        db.TicketAttachments.Should().HaveCount(1);

        var folder = Path.Combine(_tempRoot, "uploads", "tickets");
        Directory.EnumerateFiles(folder, "*.png").Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WhenAttachmentSaved_TouchesParentTicketLastUpdateMetadata()
    {
        await using var db = InMemoryDbHelper.Create();
        var ticket = BuildTicket();
        await db.Tickets.AddAsync(ticket);
        await db.SaveChangesAsync();

        var handler = new AdminUploadAttachmentHandler(
            db, CurrentUser().Object, DateTimeProvider().Object, WebEnv().Object, NoHttpContext().Object);

        await handler.Handle(new AdminUploadAttachmentCommand(
            TicketId:         ticket.Id,
            OriginalFileName: "log.txt",
            ContentType:      "text/plain",
            FileSizeBytes:    1,
            Content:          new byte[] { (byte)'y' }), CancellationToken.None);

        var updated = db.Tickets.Single();
        // The handler stamps LastUpdateDate via IDateTimeProvider, but the AuditInterceptor
        // re-stamps it on SaveChanges — so we only assert that the parent ticket was "touched"
        // (its update timestamp advanced past the original) and that the editor is recorded.
        updated.LastUpdateDate.Should().BeOnOrAfter(FixedNow);
        updated.LastUpdateBy.Should().Be("admin-001");
    }
}
