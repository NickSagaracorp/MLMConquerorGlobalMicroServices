using FluentAssertions;
using Moq;
using MLMConquerorGlobalEdition.AdminAPI.Features.Ranks.GetRankSeniorityCandidates;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using Xunit;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Ranks;

public class RankSeniorityHandlerTests
{
    // "Today" reference; the nightly job's most recent run is the day before.
    private static readonly DateTime Today = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Yesterday = Today.Date.AddDays(-1);

    private static GetRankSeniorityCandidatesHandler Handler(AppDbContext db)
    {
        var dt = new Mock<IDateTimeProvider>(); dt.Setup(d => d.Now).Returns(Today);
        return new GetRankSeniorityCandidatesHandler(db, dt.Object);
    }

    private static void SeedRankAndType(AppDbContext db, int rankId, string name, decimal amount)
    {
        db.RankDefinitions.Add(new RankDefinition { Id = rankId, Name = name, SortOrder = rankId, Status = RankDefinitionStatus.Active, CreationDate = Today, CreatedBy = "s" });
        db.CommissionTypes.Add(new CommissionType { Id = 100 + rankId, CommissionCategoryId = RankSeniorityBonus.CategoryId, Name = "Rank Seniority Bonus – " + name, LifeTimeRank = rankId, Amount = amount, IsActive = true, CreationDate = Today, CreatedBy = "s" });
        db.SaveChanges();
    }

    private static void SeedMemberAtRank(AppDbContext db, string memberId, int rankId, DateTime rankAchievedAt)
    {
        db.MemberProfiles.Add(new MemberProfile { MemberId = memberId, FirstName = "A", LastName = memberId, CreationDate = Today, CreatedBy = "s", LastUpdateDate = Today });
        db.MemberRankHistories.Add(new MemberRankHistory { MemberId = memberId, RankDefinitionId = rankId, AchievedAt = rankAchievedAt, CreationDate = rankAchievedAt, CreatedBy = "s", LastUpdateDate = rankAchievedAt });
        db.SaveChanges();
    }

    /// <summary>Adds `days` consecutive daily-residual rows at rankId ending at `endDate` (inclusive).</summary>
    private static void SeedDailyRun(AppDbContext db, string memberId, int rankId, DateTime endDate, int days)
    {
        for (var i = 0; i < days; i++)
            db.DailyResidualEarnings.Add(new DailyResidualEarning
            {
                BeneficiaryMemberId = memberId, Amount = 5m, EarnedDate = endDate.AddDays(-i),
                Status = CommissionEarningStatus.Pending, CurrentRankId = rankId, CreationDate = endDate.AddDays(-i), CreatedBy = "s"
            });
        db.SaveChanges();
    }

    [Fact]
    public async Task Member_With14ConsecutiveDays_Appears_WithBonusAmount()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedRankAndType(db, 2, "Gold", 250m);
        SeedMemberAtRank(db, "AMB-1", 2, Today.Date.AddDays(-20));
        SeedDailyRun(db, "AMB-1", 2, Yesterday, 14);

        var r = await Handler(db).Handle(new GetRankSeniorityCandidatesQuery(null, 14), CancellationToken.None);

        r.Value!.TotalCount.Should().Be(1);
        var row = r.Value.Items.Single();
        row.ConsecutiveDays.Should().Be(14);
        row.RankName.Should().Be("Gold");
        row.BonusAmount.Should().Be(250m);
    }

    [Fact]
    public async Task Member_WithGap_StreakResets_BelowThreshold_Excluded()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedRankAndType(db, 2, "Gold", 250m);
        SeedMemberAtRank(db, "AMB-1", 2, Today.Date.AddDays(-30));
        // 5-day current run, then a gap, then an older 20-day run.
        SeedDailyRun(db, "AMB-1", 2, Yesterday, 5);
        SeedDailyRun(db, "AMB-1", 2, Yesterday.AddDays(-7), 20); // separated by a gap day
        await db.SaveChangesAsync();

        var r = await Handler(db).Handle(new GetRankSeniorityCandidatesQuery(null, 14), CancellationToken.None);

        r.Value!.TotalCount.Should().Be(0); // current streak is only 5
    }

    [Fact]
    public async Task LatestDailyRankBelowLifetime_Excluded()
    {
        // Just promoted to Gold (lifetime=2) but latest daily residual is still at Silver(1) → not settled at Gold.
        await using var db = InMemoryDbHelper.Create();
        SeedRankAndType(db, 1, "Silver", 100m);
        SeedRankAndType(db, 2, "Gold", 250m);
        db.MemberProfiles.Add(new MemberProfile { MemberId = "AMB-1", FirstName = "A", LastName = "1", CreationDate = Today, CreatedBy = "s", LastUpdateDate = Today });
        db.MemberRankHistories.Add(new MemberRankHistory { MemberId = "AMB-1", RankDefinitionId = 1, AchievedAt = Today.Date.AddDays(-40), CreationDate = Today, CreatedBy = "s", LastUpdateDate = Today });
        db.MemberRankHistories.Add(new MemberRankHistory { MemberId = "AMB-1", RankDefinitionId = 2, AchievedAt = Yesterday, CreationDate = Today, CreatedBy = "s", LastUpdateDate = Today });
        db.SaveChanges();
        SeedDailyRun(db, "AMB-1", 1, Yesterday, 20); // daily rows still captured Silver

        var r = await Handler(db).Handle(new GetRankSeniorityCandidatesQuery(null, 14), CancellationToken.None);

        r.Value!.TotalCount.Should().Be(0); // lifetime Gold != latest daily Silver
    }

    [Fact]
    public async Task StreakNotEndingRecently_Excluded()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedRankAndType(db, 2, "Gold", 250m);
        SeedMemberAtRank(db, "AMB-1", 2, Today.Date.AddDays(-40));
        SeedDailyRun(db, "AMB-1", 2, Yesterday.AddDays(-10), 20); // 20-day run that ended 10 days ago

        var r = await Handler(db).Handle(new GetRankSeniorityCandidatesQuery(null, 14), CancellationToken.None);

        r.Value!.TotalCount.Should().Be(0); // not a current streak
    }

    [Fact]
    public async Task AlreadyGranted_Excluded()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedRankAndType(db, 2, "Gold", 250m);
        SeedMemberAtRank(db, "AMB-1", 2, Today.Date.AddDays(-30));
        SeedDailyRun(db, "AMB-1", 2, Yesterday, 20);
        // already received the Gold seniority bonus (CommissionTypeId 102 == 100 + rank 2)
        db.CommissionEarnings.Add(new CommissionEarning { BeneficiaryMemberId = "AMB-1", CommissionTypeId = 102, Amount = 250m, Status = CommissionEarningStatus.Paid, EarnedDate = Today.Date.AddDays(-1), CreationDate = Today, CreatedBy = "s", LastUpdateDate = Today });
        await db.SaveChangesAsync();

        var r = await Handler(db).Handle(new GetRankSeniorityCandidatesQuery(null, 14), CancellationToken.None);

        r.Value!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task FiltersByRank()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedRankAndType(db, 1, "Silver", 100m);
        SeedRankAndType(db, 2, "Gold", 250m);
        SeedMemberAtRank(db, "AMB-1", 1, Today.Date.AddDays(-30)); SeedDailyRun(db, "AMB-1", 1, Yesterday, 20);
        SeedMemberAtRank(db, "AMB-2", 2, Today.Date.AddDays(-30)); SeedDailyRun(db, "AMB-2", 2, Yesterday, 20);

        var r = await Handler(db).Handle(new GetRankSeniorityCandidatesQuery(2, 14), CancellationToken.None);

        r.Value!.TotalCount.Should().Be(1);
        r.Value.Items.Single().MemberId.Should().Be("AMB-2");
    }
}
