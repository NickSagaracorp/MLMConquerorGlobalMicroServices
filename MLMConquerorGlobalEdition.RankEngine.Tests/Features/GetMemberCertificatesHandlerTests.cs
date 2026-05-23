using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.RankEngine.Features.GetMemberCertificates;
using MLMConquerorGlobalEdition.RankEngine.Tests.Helpers;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Features;

public class GetMemberCertificatesHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    private static RankDefinition BuildRank(int id, int sortOrder, string name) => new()
    {
        Id = id, Name = name, SortOrder = sortOrder,
        Status = RankDefinitionStatus.Active, CreatedBy = "seed", CreationDate = FixedNow
    };

    private static MemberProfile BuildMember(string memberId) => new()
    {
        MemberId = memberId, FirstName = "Jane", LastName = "Doe", Email = "jane@example.com",
        MemberType = MemberType.Ambassador, EnrollDate = FixedNow.AddYears(-1), Country = "US",
        CreatedBy = "seed", LastUpdateDate = FixedNow
    };

    private static MemberRankHistory BuildHistory(string id, string memberId, int rankId,
        DateTime achievedAt, string? certUrl = null) => new()
    {
        Id = id, MemberId = memberId, RankDefinitionId = rankId, AchievedAt = achievedAt,
        GeneratedCertificateUrl = certUrl, CreatedBy = "seed",
        CreationDate = achievedAt, LastUpdateDate = achievedAt
    };

    private static GetMemberCertificatesHandler BuildHandler(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db) => new(db);

    [Fact]
    public async Task Handle_WhenMemberNotFound_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var result = await BuildHandler(db).Handle(
            new GetMemberCertificatesQuery("AMB-GHOST"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MEMBER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_ReturnsEligibleRanksWithStatus_ExcludesLifestyleConsultant()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.RankDefinitions.AddRangeAsync(
            BuildRank(20, 0, "Lifestyle Consultant"),
            BuildRank(1,  1, "Silver"),
            BuildRank(2,  2, "Gold"));
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        await db.MemberRankHistories.AddRangeAsync(
            BuildHistory("H0", "AMB-001", 20, FixedNow.AddMonths(-6)),                    // excluded
            BuildHistory("H1", "AMB-001", 1,  FixedNow.AddMonths(-3), certUrl: "url-s"),  // has cert
            BuildHistory("H2", "AMB-001", 2,  FixedNow.AddMonths(-1)));                   // no cert
        await db.SaveChangesAsync();

        var result = await BuildHandler(db).Handle(
            new GetMemberCertificatesQuery("AMB-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
        result.Value!.Should().NotContain(d => d.RankName == "Lifestyle Consultant");

        var silver = result.Value!.Single(d => d.RankName == "Silver");
        silver.HasCertificate.Should().BeTrue();
        silver.CertificateUrl.Should().Be("url-s");

        var gold = result.Value!.Single(d => d.RankName == "Gold");
        gold.HasCertificate.Should().BeFalse();
        gold.CertificateUrl.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenRankAchievedTwice_ReturnsEarliestRecordOnly()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.RankDefinitions.AddAsync(BuildRank(1, 1, "Silver"));
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        var early = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var late  = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc);
        await db.MemberRankHistories.AddRangeAsync(
            BuildHistory("H1", "AMB-001", 1, early, certUrl: "url-first"),
            BuildHistory("H2", "AMB-001", 1, late));
        await db.SaveChangesAsync();

        var result = await BuildHandler(db).Handle(
            new GetMemberCertificatesQuery("AMB-001"), CancellationToken.None);

        result.Value!.Should().HaveCount(1);
        result.Value![0].MemberRankHistoryId.Should().Be("H1");
        result.Value![0].FirstAchievedAt.Should().Be(early);
    }
}
