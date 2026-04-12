using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.RankEngine.Features.GenerateCertificate;
using MLMConquerorGlobalEdition.RankEngine.Services;
using MLMConquerorGlobalEdition.RankEngine.Tests.Helpers;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Features;

public class GenerateCertificateHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> BuildClock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static Mock<ICurrentUserService> BuildUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("system");
        return m;
    }

    private static Mock<ICertificatePdfFillerService> BuildPdfFiller(byte[]? bytes = null)
    {
        var m = new Mock<ICertificatePdfFillerService>();
        m.Setup(p => p.FillAsync(
                It.IsAny<int>(),
                It.IsAny<CertificateTemplateData>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes ?? new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF magic bytes
        return m;
    }

    private static Mock<IS3FileService> BuildS3(string url = "https://s3.example.com/cert.pdf")
    {
        var m = new Mock<IS3FileService>();
        m.Setup(s => s.UploadAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(url);
        return m;
    }

    private GenerateCertificateHandler BuildHandler(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db,
        Mock<ICertificatePdfFillerService>? pdfFiller = null,
        Mock<IS3FileService>? s3 = null) =>
        new(db,
            (pdfFiller ?? BuildPdfFiller()).Object,
            (s3 ?? BuildS3()).Object,
            BuildClock().Object,
            BuildUser().Object);

    private static MemberProfile BuildMember(string memberId) => new()
    {
        MemberId       = memberId,
        FirstName      = "Jane",
        LastName       = "Doe",
        Email          = "jane@example.com",
        MemberType     = MemberType.Ambassador,
        EnrollDate     = FixedNow.AddYears(-1),
        Country        = "US",
        CreatedBy      = "seed",
        LastUpdateDate = FixedNow
    };

    private static RankDefinition BuildRank(int id, int sortOrder) => new()
    {
        Id           = id,
        Name         = $"Rank-{sortOrder}",
        SortOrder    = sortOrder,
        Status       = RankDefinitionStatus.Active,
        CreatedBy    = "seed",
        CreationDate = FixedNow
    };

    private static MemberRankHistory BuildHistory(string id, string memberId, int rankId,
        string? certUrl = null) => new()
    {
        Id                      = id,
        MemberId                = memberId,
        RankDefinitionId        = rankId,
        AchievedAt              = FixedNow.AddMonths(-1),
        GeneratedCertificateUrl = certUrl,
        CreatedBy               = "seed",
        CreationDate            = FixedNow.AddMonths(-1),
        LastUpdateDate          = FixedNow.AddMonths(-1)
    };

    [Fact]
    public async Task Handle_WhenHistoryNotFound_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = BuildHandler(db);

        var result = await handler.Handle(
            new GenerateCertificateCommand("HIST-GHOST"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("RANK_HISTORY_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenHistoryIsSoftDeleted_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.RankDefinitions.AddAsync(BuildRank(1, sortOrder: 1));
        var history = BuildHistory("HIST-001", "AMB-001", rankId: 1);
        history.IsDeleted = true;
        await db.MemberRankHistories.AddAsync(history);
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);
        var result  = await handler.Handle(
            new GenerateCertificateCommand("HIST-001"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("RANK_HISTORY_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenCertificateAlreadyExistsOnThisRecord_ReturnsCachedUrl()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.RankDefinitions.AddAsync(BuildRank(1, sortOrder: 1));
        await db.MemberRankHistories.AddAsync(
            BuildHistory("HIST-001", "AMB-001", rankId: 1,
                certUrl: "https://s3.example.com/existing.pdf"));
        await db.SaveChangesAsync();

        var s3      = BuildS3();
        var handler = BuildHandler(db, s3: s3);
        var result  = await handler.Handle(
            new GenerateCertificateCommand("HIST-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CertificateUrl.Should().Be("https://s3.example.com/existing.pdf");
        // S3 upload must not be called again
        s3.Verify(s => s.UploadAsync(
            It.IsAny<string>(), It.IsAny<Stream>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenEarlierRecordForSameRankHasCert_ReturnsThatUrl()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.RankDefinitions.AddAsync(BuildRank(1, sortOrder: 1));
        // Earlier history record already has a cert
        await db.MemberRankHistories.AddRangeAsync(
            BuildHistory("HIST-001", "AMB-001", rankId: 1,
                certUrl: "https://s3.example.com/first.pdf"),
            BuildHistory("HIST-002", "AMB-001", rankId: 1,
                certUrl: null));
        await db.SaveChangesAsync();

        var s3      = BuildS3();
        var handler = BuildHandler(db, s3: s3);
        // Request cert for the second history record
        var result  = await handler.Handle(
            new GenerateCertificateCommand("HIST-002"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CertificateUrl.Should().Be("https://s3.example.com/first.pdf");
        s3.Verify(s => s.UploadAsync(
            It.IsAny<string>(), It.IsAny<Stream>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMemberNotFound_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.RankDefinitions.AddAsync(BuildRank(1, sortOrder: 1));
        // History exists but no MemberProfile seeded
        await db.MemberRankHistories.AddAsync(BuildHistory("HIST-001", "AMB-MISSING", rankId: 1));
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);
        var result  = await handler.Handle(
            new GenerateCertificateCommand("HIST-001"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MEMBER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenNoCertificateExists_GeneratesAndPersistsCertificateUrl()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.RankDefinitions.AddAsync(BuildRank(1, sortOrder: 1));
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        await db.MemberRankHistories.AddAsync(BuildHistory("HIST-001", "AMB-001", rankId: 1));
        await db.SaveChangesAsync();

        const string expectedUrl = "https://s3.example.com/generated.pdf";
        var handler = BuildHandler(db, s3: BuildS3(expectedUrl));
        var result  = await handler.Handle(
            new GenerateCertificateCommand("HIST-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CertificateUrl.Should().Be(expectedUrl);
        result.Value.MemberId.Should().Be("AMB-001");

        // Persisted to DB
        var savedHistory = db.MemberRankHistories.Single(h => h.Id == "HIST-001");
        savedHistory.GeneratedCertificateUrl.Should().Be(expectedUrl);
    }
}
