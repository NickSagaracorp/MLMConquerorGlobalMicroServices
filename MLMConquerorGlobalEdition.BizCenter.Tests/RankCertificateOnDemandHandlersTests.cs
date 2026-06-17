using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Features.Ranks.DownloadCertificate;
using MLMConquerorGlobalEdition.BizCenter.Features.Ranks.GenerateCertificateOnDemand;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// Tests for on-demand certificate generation (member-initiated) and lazy
/// generation behavior on download. The RankEngine HTTP client is fully
/// mocked — we verify the BizCenter side enforces ownership, relays the
/// bearer token, and reacts correctly to success/failure responses.
/// </summary>
public class RankCertificateOnDemandHandlersTests : IDisposable
{
    private const string MemberId   = "AMB-OWNER";
    private const string OtherMember = "AMB-OTHER";
    private const string Bearer     = "fake.jwt.token";

    private readonly AppDbContext _db;
    private readonly Mock<ICurrentUserService> _currentUser;
    private readonly Mock<IRankEngineClient> _rankEngine;

    public RankCertificateOnDemandHandlersTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _currentUser = new Mock<ICurrentUserService>();
        _currentUser.Setup(x => x.MemberId).Returns(MemberId);

        _rankEngine = new Mock<IRankEngineClient>();
    }

    public void Dispose() => _db.Dispose();

    private async Task SeedOwnedHistoryAsync(string id, string? certUrl = null)
    {
        await _db.MemberRankHistories.AddAsync(new MemberRankHistory
        {
            Id                      = id,
            MemberId                = MemberId,
            RankDefinitionId        = 1,
            AchievedAt              = DateTime.UtcNow,
            GeneratedCertificateUrl = certUrl,
            CreatedBy               = "seed",
            CreationDate            = DateTime.UtcNow,
            LastUpdateDate          = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private async Task SeedOtherMemberHistoryAsync(string id)
    {
        await _db.MemberRankHistories.AddAsync(new MemberRankHistory
        {
            Id               = id,
            MemberId         = OtherMember,
            RankDefinitionId = 1,
            AchievedAt       = DateTime.UtcNow,
            CreatedBy        = "seed",
            CreationDate     = DateTime.UtcNow,
            LastUpdateDate   = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    // ── GenerateCertificateOnDemandHandler ─────────────────────────────────────

    [Fact]
    public async Task GenerateOnDemand_WhenOwnedAndRankEngineSucceeds_ReturnsCertificateUrl()
    {
        await SeedOwnedHistoryAsync("HIST-1");

        const string url = "https://s3.local/cert-HIST-1.pdf";
        _rankEngine.Setup(c => c.GenerateMemberCertificateAsync(
                "HIST-1", Bearer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(url));

        var handler = new GenerateCertificateOnDemandHandler(
            _db, _currentUser.Object, _rankEngine.Object);

        var result = await handler.Handle(
            new GenerateCertificateOnDemandCommand("HIST-1", Bearer), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(url);
        _rankEngine.Verify(c => c.GenerateMemberCertificateAsync(
            "HIST-1", Bearer, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateOnDemand_WhenRecordNotOwnedByCaller_ReturnsNotFoundWithoutCallingRankEngine()
    {
        await SeedOtherMemberHistoryAsync("HIST-OTHER");

        var handler = new GenerateCertificateOnDemandHandler(
            _db, _currentUser.Object, _rankEngine.Object);

        var result = await handler.Handle(
            new GenerateCertificateOnDemandCommand("HIST-OTHER", Bearer), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("RANK_HISTORY_NOT_FOUND");

        // Critical: we must NOT proxy a forbidden request through to RankEngine —
        // BizCenter is the first line of defense against cross-member access.
        _rankEngine.Verify(c => c.GenerateMemberCertificateAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateOnDemand_WhenRecordDoesNotExist_ReturnsNotFound()
    {
        var handler = new GenerateCertificateOnDemandHandler(
            _db, _currentUser.Object, _rankEngine.Object);

        var result = await handler.Handle(
            new GenerateCertificateOnDemandCommand("HIST-MISSING", Bearer), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("RANK_HISTORY_NOT_FOUND");
    }

    [Fact]
    public async Task GenerateOnDemand_WhenRankEngineFails_PropagatesFailure()
    {
        await SeedOwnedHistoryAsync("HIST-2");

        _rankEngine.Setup(c => c.GenerateMemberCertificateAsync(
                "HIST-2", Bearer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure(
                "RANK_NOT_CERTIFICATE_ELIGIBLE", "Not eligible"));

        var handler = new GenerateCertificateOnDemandHandler(
            _db, _currentUser.Object, _rankEngine.Object);

        var result = await handler.Handle(
            new GenerateCertificateOnDemandCommand("HIST-2", Bearer), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("RANK_NOT_CERTIFICATE_ELIGIBLE");
    }

    // ── DownloadCertificateHandler (lazy generation) ───────────────────────────

    [Fact]
    public async Task Download_WhenCertificateExists_ReturnsExistingUrl_NoGeneration()
    {
        const string existing = "https://s3.local/cached.pdf";
        await SeedOwnedHistoryAsync("HIST-CACHED", certUrl: existing);

        var handler = new DownloadCertificateHandler(
            _db, _currentUser.Object, _rankEngine.Object);

        var result = await handler.Handle(
            new DownloadCertificateQuery("HIST-CACHED", Bearer), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(existing);
        _rankEngine.Verify(c => c.GenerateMemberCertificateAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Download_WhenCertificateMissing_TriggersLazyGenerationAndReturnsNewUrl()
    {
        await SeedOwnedHistoryAsync("HIST-LAZY"); // GeneratedCertificateUrl is null

        const string generated = "https://s3.local/lazy-HIST-LAZY.pdf";
        _rankEngine.Setup(c => c.GenerateMemberCertificateAsync(
                "HIST-LAZY", Bearer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(generated));

        var handler = new DownloadCertificateHandler(
            _db, _currentUser.Object, _rankEngine.Object);

        var result = await handler.Handle(
            new DownloadCertificateQuery("HIST-LAZY", Bearer), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(generated);
        _rankEngine.Verify(c => c.GenerateMemberCertificateAsync(
            "HIST-LAZY", Bearer, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Download_WhenCertificateMissingAndNoBearerToken_ReturnsAuthError()
    {
        await SeedOwnedHistoryAsync("HIST-NO-BEARER");

        var handler = new DownloadCertificateHandler(
            _db, _currentUser.Object, _rankEngine.Object);

        var result = await handler.Handle(
            new DownloadCertificateQuery("HIST-NO-BEARER"), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("AUTH_TOKEN_MISSING");
    }

    [Fact]
    public async Task Download_WhenRecordNotOwned_ReturnsNotFound()
    {
        await SeedOtherMemberHistoryAsync("HIST-FOREIGN");

        var handler = new DownloadCertificateHandler(
            _db, _currentUser.Object, _rankEngine.Object);

        var result = await handler.Handle(
            new DownloadCertificateQuery("HIST-FOREIGN", Bearer), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("RANK_HISTORY_NOT_FOUND");
    }

    [Fact]
    public async Task Download_WhenLazyGenerationFails_PropagatesFailure()
    {
        await SeedOwnedHistoryAsync("HIST-FAIL");

        _rankEngine.Setup(c => c.GenerateMemberCertificateAsync(
                "HIST-FAIL", Bearer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure(
                "RANK_ENGINE_UNAVAILABLE", "Service down"));

        var handler = new DownloadCertificateHandler(
            _db, _currentUser.Object, _rankEngine.Object);

        var result = await handler.Handle(
            new DownloadCertificateQuery("HIST-FAIL", Bearer), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("RANK_ENGINE_UNAVAILABLE");
    }
}
