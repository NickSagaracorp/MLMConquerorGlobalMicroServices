using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.RankEngine.Features.DeleteCertificate;
using MLMConquerorGlobalEdition.RankEngine.Services;
using MLMConquerorGlobalEdition.RankEngine.Tests.Helpers;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Features;

public class DeleteCertificateHandlerTests
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
        m.Setup(u => u.UserId).Returns("admin");
        return m;
    }

    private static RankDefinition BuildRank(int id, int sortOrder) => new()
    {
        Id = id, Name = $"Rank-{sortOrder}", SortOrder = sortOrder,
        Status = RankDefinitionStatus.Active, CreatedBy = "seed", CreationDate = FixedNow
    };

    private static MemberRankHistory BuildHistory(string id, string memberId, int rankId,
        string? certUrl = null) => new()
    {
        Id = id, MemberId = memberId, RankDefinitionId = rankId,
        AchievedAt = FixedNow.AddMonths(-1), GeneratedCertificateUrl = certUrl,
        CreatedBy = "seed", CreationDate = FixedNow.AddMonths(-1), LastUpdateDate = FixedNow.AddMonths(-1)
    };

    private static DeleteCertificateHandler BuildHandler(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db,
        Mock<ICertificateStorage>? storage = null) =>
        new(db, (storage ?? new Mock<ICertificateStorage>()).Object,
            BuildClock().Object, BuildUser().Object);

    [Fact]
    public async Task Handle_WhenHistoryNotFound_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = BuildHandler(db);

        var result = await handler.Handle(
            new DeleteCertificateCommand("HIST-GHOST"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("RANK_HISTORY_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenNoCertificateExists_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.RankDefinitions.AddAsync(BuildRank(1, 1));
        await db.MemberRankHistories.AddAsync(BuildHistory("HIST-001", "AMB-001", 1, certUrl: null));
        await db.SaveChangesAsync();

        var result = await BuildHandler(db).Handle(
            new DeleteCertificateCommand("HIST-001"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CERTIFICATE_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenCertificateExists_DeletesFileAndClearsUrl()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.RankDefinitions.AddAsync(BuildRank(1, 1));
        await db.MemberRankHistories.AddAsync(
            BuildHistory("HIST-001", "AMB-001", 1,
                certUrl: "https://localhost:7009/certificates/abc_AMB-001_Silver.pdf"));
        await db.SaveChangesAsync();

        var storage = new Mock<ICertificateStorage>();
        var result  = await BuildHandler(db, storage).Handle(
            new DeleteCertificateCommand("HIST-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        storage.Verify(s => s.DeleteAsync(
            "abc_AMB-001_Silver.pdf", It.IsAny<CancellationToken>()), Times.Once);
        db.MemberRankHistories.Single(h => h.Id == "HIST-001")
            .GeneratedCertificateUrl.Should().BeNull();
    }
}
