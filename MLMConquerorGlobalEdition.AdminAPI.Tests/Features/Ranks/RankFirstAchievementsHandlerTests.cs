using FluentAssertions;
using MLMConquerorGlobalEdition.AdminAPI.Features.Ranks.GetRankFirstAchievements;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Repository.Context;
using Xunit;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Ranks;

public class RankFirstAchievementsHandlerTests
{
    private static readonly DateTime Seed = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static void SeedRanks(AppDbContext db)
    {
        db.RankDefinitions.Add(new RankDefinition { Id = 1, Name = "Silver", SortOrder = 1, Status = RankDefinitionStatus.Active, CreationDate = Seed, CreatedBy = "s" });
        db.RankDefinitions.Add(new RankDefinition { Id = 2, Name = "Gold", SortOrder = 2, Status = RankDefinitionStatus.Active, CreationDate = Seed, CreatedBy = "s" });
        db.SaveChanges();
    }

    private static void SeedMember(AppDbContext db, string memberId, string first, string last)
        => db.MemberProfiles.Add(new MemberProfile { MemberId = memberId, FirstName = first, LastName = last, CreationDate = Seed, CreatedBy = "s", LastUpdateDate = Seed });

    private static void SeedHistory(AppDbContext db, string memberId, int rankId, DateTime achievedAt, int? previousRankId = null)
        => db.MemberRankHistories.Add(new MemberRankHistory { MemberId = memberId, RankDefinitionId = rankId, PreviousRankId = previousRankId, AchievedAt = achievedAt, CreationDate = achievedAt, CreatedBy = "s", LastUpdateDate = achievedAt });

    [Fact]
    public async Task ReturnsFirstAchievement_InSelectedMonth()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedRanks(db); SeedMember(db, "AMB-1", "Ana", "Diaz");
        SeedHistory(db, "AMB-1", 2, new DateTime(2026, 6, 15)); // Gold in June
        await db.SaveChangesAsync();

        var r = await new GetRankFirstAchievementsHandler(db).Handle(
            new GetRankFirstAchievementsQuery(2026, 6, null), CancellationToken.None);

        r.Value!.TotalCount.Should().Be(1);
        r.Value.Items.Single().RankName.Should().Be("Gold");
        r.Value.Items.Single().FullName.Should().Be("Ana Diaz");
    }

    [Fact]
    public async Task ExcludesRank_FirstAchievedInEarlierMonth_EvenIfReachedAgainLater()
    {
        // The Gold→Silver→Gold case: a member whose FIRST Gold was in May must not appear for June,
        // regardless of any later Gold row. MIN(AchievedAt) is the rule.
        await using var db = InMemoryDbHelper.Create();
        SeedRanks(db); SeedMember(db, "AMB-1", "Ana", "Diaz");
        SeedHistory(db, "AMB-1", 2, new DateTime(2026, 5, 10)); // first Gold = May
        SeedHistory(db, "AMB-1", 2, new DateTime(2026, 6, 20)); // a later Gold row (defensive)
        await db.SaveChangesAsync();

        var r = await new GetRankFirstAchievementsHandler(db).Handle(
            new GetRankFirstAchievementsQuery(2026, 6, null), CancellationToken.None);

        r.Value!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task FiltersByRank()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedRanks(db); SeedMember(db, "AMB-1", "Ana", "Diaz");
        SeedHistory(db, "AMB-1", 1, new DateTime(2026, 6, 5));  // Silver in June
        SeedHistory(db, "AMB-1", 2, new DateTime(2026, 6, 25)); // Gold in June
        await db.SaveChangesAsync();

        var r = await new GetRankFirstAchievementsHandler(db).Handle(
            new GetRankFirstAchievementsQuery(2026, 6, 2), CancellationToken.None);

        r.Value!.TotalCount.Should().Be(1);
        r.Value.Items.Single().RankDefinitionId.Should().Be(2);
    }

    [Fact]
    public async Task MultipleFirstRanksInSameMonth_AllAppear()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedRanks(db); SeedMember(db, "AMB-1", "Ana", "Diaz");
        SeedHistory(db, "AMB-1", 1, new DateTime(2026, 6, 5));
        SeedHistory(db, "AMB-1", 2, new DateTime(2026, 6, 25));
        await db.SaveChangesAsync();

        var r = await new GetRankFirstAchievementsHandler(db).Handle(
            new GetRankFirstAchievementsQuery(2026, 6, null), CancellationToken.None);

        r.Value!.TotalCount.Should().Be(2); // both first-achievements that month
    }

    [Fact]
    public async Task ReturnsPreviousRankName_WhenFirstAchievementHasPreviousRankId()
    {
        // Member first achieved Gold (rank 2) in June, coming from Silver (rank 1).
        await using var db = InMemoryDbHelper.Create();
        SeedRanks(db); SeedMember(db, "AMB-1", "Ana", "Diaz");
        SeedHistory(db, "AMB-1", 2, new DateTime(2026, 6, 20), previousRankId: 1); // Gold, came from Silver
        await db.SaveChangesAsync();

        var r = await new GetRankFirstAchievementsHandler(db).Handle(
            new GetRankFirstAchievementsQuery(2026, 6, null), CancellationToken.None);

        r.Value!.TotalCount.Should().Be(1);
        var row = r.Value.Items.Single();
        row.RankName.Should().Be("Gold");
        row.PreviousRankName.Should().Be("Silver");
    }
}
