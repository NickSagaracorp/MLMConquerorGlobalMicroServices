using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Features.Tickets.UploadAttachment;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// Covers the BizCenter ticket-attachment upload handler. Files are written to a
/// per-test temp directory injected via <see cref="IWebHostEnvironment.WebRootPath"/>
/// so each test cleans up after itself and there is no contention.
/// </summary>
public class UploadAttachmentHandlerTests : IDisposable
{
    private const string MemberId = "member-att-001";
    private const string UserId   = "user-att-001";

    private readonly AppDbContext _db;
    private readonly Mock<ICurrentUserService> _currentUser;
    private readonly Mock<IDateTimeProvider> _dateTime;
    private readonly Mock<IWebHostEnvironment> _env;
    private readonly Mock<IHttpContextAccessor> _httpContext;
    private readonly string _tempRoot;

    public UploadAttachmentHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _currentUser = new Mock<ICurrentUserService>();
        _currentUser.Setup(x => x.MemberId).Returns(MemberId);
        _currentUser.Setup(x => x.UserId).Returns(UserId);

        _dateTime = new Mock<IDateTimeProvider>();
        _dateTime.Setup(x => x.UtcNow).Returns(new DateTime(2026, 5, 6, 15, 0, 0, DateTimeKind.Utc));

        _tempRoot = Path.Combine(Path.GetTempPath(), $"mlm-attach-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);

        _env = new Mock<IWebHostEnvironment>();
        _env.Setup(x => x.WebRootPath).Returns(_tempRoot);

        _httpContext = new Mock<IHttpContextAccessor>();
        _httpContext.Setup(x => x.HttpContext).Returns((HttpContext?)null);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true);
    }

    private Ticket SeedTicket(string memberId = MemberId)
    {
        var t = new Ticket
        {
            MemberId       = memberId,
            CategoryId     = 1,
            Subject        = "Sample",
            Body           = "Sample body",
            Status         = TicketStatus.Open,
            Priority       = TicketPriority.Normal,
            CreationDate   = DateTime.UtcNow.AddDays(-1),
            LastUpdateDate = DateTime.UtcNow.AddDays(-1),
            CreatedBy      = "seed"
        };
        _db.Tickets.Add(t);
        _db.SaveChanges();
        return t;
    }

    [Fact]
    public async Task Handle_WhenTicketBelongsToCurrentMember_PersistsAttachmentAndFile()
    {
        var ticket = SeedTicket();
        var handler = new UploadAttachmentHandler(_db, _currentUser.Object, _dateTime.Object, _env.Object, _httpContext.Object);

        var bytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // "%PDF" header — content-validation is the controller's job
        var result = await handler.Handle(new UploadAttachmentCommand(
            TicketId:         ticket.Id,
            OriginalFileName: "report.pdf",
            ContentType:      "application/pdf",
            FileSizeBytes:    bytes.Length,
            Content:          bytes), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FileName.Should().Be("report.pdf");
        result.Value.FileSizeBytes.Should().Be(4);
        result.Value.ContentType.Should().Be("application/pdf");
        // FileUrl is server-relative; with no HttpContext, the DownloadUrl falls back to that path.
        result.Value.DownloadUrl.Should().StartWith("/uploads/tickets/");
        result.Value.UploadedBy.Should().Be(UserId);

        var stored = _db.TicketAttachments.Single();
        stored.TicketId.Should().Be(ticket.Id);
        stored.FileSizeBytes.Should().Be(4);

        // The disk-name is a GUID, but the directory + extension are deterministic.
        var folder = Path.Combine(_tempRoot, "uploads", "tickets");
        Directory.Exists(folder).Should().BeTrue();
        Directory.EnumerateFiles(folder, "*.pdf").Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WhenTicketDoesNotExist_ReturnsTicketNotFoundFailure()
    {
        var handler = new UploadAttachmentHandler(_db, _currentUser.Object, _dateTime.Object, _env.Object, _httpContext.Object);

        var result = await handler.Handle(new UploadAttachmentCommand(
            TicketId:         "ghost-ticket",
            OriginalFileName: "nope.pdf",
            ContentType:      "application/pdf",
            FileSizeBytes:    1,
            Content:          new byte[] { 0x00 }), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TICKET_NOT_FOUND");
        _db.TicketAttachments.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTicketBelongsToAnotherMember_ReturnsTicketNotFoundFailure()
    {
        // Ticket owned by a *different* member — the per-member filter in the handler
        // intentionally surfaces this as TICKET_NOT_FOUND (no leakage).
        var ticket = SeedTicket(memberId: "other-member-999");
        var handler = new UploadAttachmentHandler(_db, _currentUser.Object, _dateTime.Object, _env.Object, _httpContext.Object);

        var result = await handler.Handle(new UploadAttachmentCommand(
            TicketId:         ticket.Id,
            OriginalFileName: "snoop.pdf",
            ContentType:      "application/pdf",
            FileSizeBytes:    1,
            Content:          new byte[] { 0x00 }), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TICKET_NOT_FOUND");
        _db.TicketAttachments.Should().BeEmpty();
    }
}
