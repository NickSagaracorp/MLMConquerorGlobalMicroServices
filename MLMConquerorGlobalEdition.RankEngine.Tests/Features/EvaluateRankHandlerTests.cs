using Hangfire;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.RankEngine.Features.EvaluateRank;
using MLMConquerorGlobalEdition.RankEngine.Services;
using MLMConquerorGlobalEdition.RankEngine.Tests.Helpers;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Features;

public class EvaluateRankHandlerTests
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

    private static Mock<ICacheService> BuildCache()
    {
        var m = new Mock<ICacheService>();
        m.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return m;
    }

    /// <summary>
    /// IBackgroundJobClient mock — Enqueue&lt;T&gt; is an extension that calls Create(...)
    /// internally; we stub Create to return a fake job id so the extension is happy.
    /// </summary>
    private static Mock<IBackgroundJobClient> BuildJobs()
    {
        var m = new Mock<IBackgroundJobClient>();
        m.Setup(c => c.Create(
                It.IsAny<Hangfire.Common.Job>(),
                It.IsAny<Hangfire.States.IState>()))
            .Returns("test-job-id");
        return m;
    }

    private static IRankQualificationService BuildQualification(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db)
    {
        var et = new EnrollmentTeamPointsService(db);
        var pcp = new PersonalCustomerPointsService(db);
        return new RankQualificationService(db, et, pcp);
    }

    private EvaluateRankHandler BuildHandler(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db)
    {
        return new EvaluateRankHandler(
            db,
            BuildClock().Object,
            BuildUser().Object,
            BuildQualification(db),
            BuildCache().Object,
            BuildJobs().Object);
    }

    /// <summary>Gives a member an Active membership worth enough PCP to clear the gate (>= 12).</summary>
    private static async Task SatisfyGateAsync(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db, string memberId)
    {
        var orderId = $"ORD-{memberId}";
        await db.Orders.AddAsync(new MLMConquerorGlobalEdition.Domain.Entities.Orders.Orders
        {
            Id = orderId, MemberId = memberId,
            Status = MLMConquerorGlobalEdition.Domain.Entities.Orders.OrderStatus.Completed,
            OrderDate = FixedNow, CreatedBy = "seed", CreationDate = FixedNow, LastUpdateDate = FixedNow
        });
        await db.Products.AddAsync(new MLMConquerorGlobalEdition.Domain.Entities.Orders.Product
        {
            Id = $"PRD-{memberId}", Name = "Membership", Description = "d", ImageUrl = "x",
            MonthlyFee = 0, SetupFee = 0, QualificationPoins = 12,
            CreatedBy = "seed", CreationDate = FixedNow, LastUpdateDate = FixedNow
        });
        await db.OrderDetails.AddAsync(new MLMConquerorGlobalEdition.Domain.Entities.Orders.OrderDetail
        {
            OrderId = orderId, ProductId = $"PRD-{memberId}", Quantity = 1, UnitPrice = 0,
            CreatedBy = "seed", CreationDate = FixedNow
        });
        await db.MembershipSubscriptions.AddAsync(
            new MLMConquerorGlobalEdition.Domain.Entities.Membership.MembershipSubscription
            {
                MemberId = memberId, MembershipLevelId = 1,
                SubscriptionStatus = MLMConquerorGlobalEdition.Domain.Entities.Membership.MembershipStatus.Active,
                StartDate = FixedNow, LastOrderId = orderId,
                CreatedBy = "seed", CreationDate = FixedNow, LastUpdateDate = FixedNow
            });
        await db.SaveChangesAsync();
    }

    private static MemberProfile BuildMember(string memberId) => new()
    {
        MemberId       = memberId,
        FirstName      = "Test",
        LastName       = "User",
        Email          = "test@example.com",
        MemberType     = MemberType.Ambassador,
        EnrollDate     = FixedNow.AddMonths(-6),
        Country        = "US",
        CreatedBy      = "seed",
        LastUpdateDate = FixedNow
    };

    private static RankDefinition BuildRank(int id, int sortOrder,
        int personalPointsReq = 0, int sponsoredMembersReq = 0) => new()
    {
        Id           = id,
        Name         = $"Rank-{sortOrder}",
        SortOrder    = sortOrder,
        Status       = RankDefinitionStatus.Active,
        CreatedBy    = "seed",
        CreationDate = FixedNow,
        Requirements = new List<RankRequirement>
        {
            new()
            {
                Id               = id * 100,
                RankDefinitionId = id,
                LevelNo          = 0,
                PersonalPoints   = personalPointsReq,
                SponsoredMembers = sponsoredMembersReq,
                ExternalMembers  = 0,
                CreatedBy        = "seed",
                CreationDate     = FixedNow
            }
        }
    };

    [Fact]
    public async Task Handle_WhenMemberNotFound_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = BuildHandler(db);

        var result = await handler.Handle(
            new EvaluateRankCommand("AMB-GHOST"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MEMBER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenNoHigherRanksAvailable_RankAchievedIsFalse()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        var rank = BuildRank(1, sortOrder: 1);
        await db.RankDefinitions.AddAsync(rank);
        await db.SaveChangesAsync();

        // Member already has the highest rank
        await db.MemberRankHistories.AddAsync(new Domain.Entities.Rank.MemberRankHistory
        {
            MemberId         = "AMB-001",
            RankDefinitionId = 1,
            AchievedAt       = FixedNow.AddMonths(-1),
            CreatedBy        = "seed",
            CreationDate     = FixedNow.AddMonths(-1),
            LastUpdateDate   = FixedNow.AddMonths(-1)
        });
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);
        var result  = await handler.Handle(
            new EvaluateRankCommand("AMB-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RankAchieved.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenMemberDoesNotMeetRequirements_RankAchievedIsFalse()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        // Requires 100 personal points; member has none
        await db.RankDefinitions.AddAsync(BuildRank(1, sortOrder: 1, personalPointsReq: 100));
        await db.SaveChangesAsync();
        // Gate satisfied — this test isolates the PersonalPoints axis failure.
        await SatisfyGateAsync(db, "AMB-001");

        var handler = BuildHandler(db);
        var result  = await handler.Handle(
            new EvaluateRankCommand("AMB-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RankAchieved.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenMemberMeetsRequirements_AchievesRankAndPersistsHistory()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        // Rank requires 0 personal points and 0 sponsored members — member qualifies immediately
        await db.RankDefinitions.AddAsync(BuildRank(1, sortOrder: 1, personalPointsReq: 0));
        await db.SaveChangesAsync();
        await SatisfyGateAsync(db, "AMB-001");

        var handler = BuildHandler(db);
        var result  = await handler.Handle(
            new EvaluateRankCommand("AMB-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RankAchieved.Should().BeTrue();
        result.Value.AchievedRank!.SortOrder.Should().Be(1);

        var history = db.MemberRankHistories.ToList();
        history.Should().HaveCount(1);
        history[0].MemberId.Should().Be("AMB-001");
        history[0].RankDefinitionId.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenMultipleRanksQualified_AchievesHighest()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        // Two ranks with zero requirements — member qualifies for both
        await db.RankDefinitions.AddRangeAsync(
            BuildRank(1, sortOrder: 1, personalPointsReq: 0),
            BuildRank(2, sortOrder: 2, personalPointsReq: 0));
        await db.SaveChangesAsync();
        await SatisfyGateAsync(db, "AMB-001");

        var handler = BuildHandler(db);
        var result  = await handler.Handle(
            new EvaluateRankCommand("AMB-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RankAchieved.Should().BeTrue();
        // Headline rank in the response is rank 2 (the highest qualifying)
        result.Value.AchievedRank!.SortOrder.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WhenSkippingMultipleRanks_PersistsChainedIntermediateHistoryRows()
    {
        // A member with no rank history qualifies for ranks 1..5 in one evaluation.
        // The handler must persist 5 MemberRankHistory rows (one per intermediate rank)
        // with PreviousRankId chained so the promotion story is preserved end-to-end.
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        await db.RankDefinitions.AddRangeAsync(
            BuildRank(1, sortOrder: 1),
            BuildRank(2, sortOrder: 2),
            BuildRank(3, sortOrder: 3),
            BuildRank(4, sortOrder: 4),
            BuildRank(5, sortOrder: 5));
        await db.SaveChangesAsync();
        await SatisfyGateAsync(db, "AMB-001");

        var handler = BuildHandler(db);
        var result  = await handler.Handle(
            new EvaluateRankCommand("AMB-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RankAchieved.Should().BeTrue();
        result.Value.AchievedRank!.SortOrder.Should().Be(5);

        var history = db.MemberRankHistories
            .Include(h => h.RankDefinition)
            .OrderBy(h => h.RankDefinition!.SortOrder)
            .ToList();

        history.Should().HaveCount(5);
        history.Select(h => h.RankDefinitionId).Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 });

        // PreviousRankId chain: 1 → null, 2 → 1, 3 → 2, 4 → 3, 5 → 4
        history[0].PreviousRankId.Should().BeNull();
        history[1].PreviousRankId.Should().Be(1);
        history[2].PreviousRankId.Should().Be(2);
        history[3].PreviousRankId.Should().Be(3);
        history[4].PreviousRankId.Should().Be(4);

        // Every intermediate row has NO certificate URL — certs are generated on demand.
        history.Should().OnlyContain(h => h.GeneratedCertificateUrl == null);
    }

    [Fact]
    public async Task Handle_WhenEvaluationSucceeds_DoesNotAutoGenerateCertificate()
    {
        // Verifies the cert auto-generation has been removed: a successful promotion
        // leaves GeneratedCertificateUrl null on every row. The on-demand path
        // (BizCenter / Admin) is the only way certs are produced now.
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        await db.RankDefinitions.AddAsync(BuildRank(1, sortOrder: 1));
        await db.SaveChangesAsync();
        await SatisfyGateAsync(db, "AMB-001");

        var handler = BuildHandler(db);
        await handler.Handle(new EvaluateRankCommand("AMB-001"), CancellationToken.None);

        var rows = db.MemberRankHistories.ToList();
        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(h => h.GeneratedCertificateUrl == null,
            "EvaluateRank no longer auto-generates certificates — they are minted on demand.");
    }

    [Fact]
    public async Task Handle_WhenSkippingFromExistingRank_ChainsFromCurrentRank()
    {
        // Member currently holds rank 2 (SortOrder 2) and now qualifies up to rank 5.
        // Only intermediate rows for 3, 4, 5 should be created, with PreviousRankId
        // starting from the current rank's id (2).
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        await db.RankDefinitions.AddRangeAsync(
            BuildRank(1, sortOrder: 1),
            BuildRank(2, sortOrder: 2),
            BuildRank(3, sortOrder: 3),
            BuildRank(4, sortOrder: 4),
            BuildRank(5, sortOrder: 5));
        await db.MemberRankHistories.AddAsync(new MemberRankHistory
        {
            MemberId         = "AMB-001",
            RankDefinitionId = 2,
            AchievedAt       = FixedNow.AddDays(-30),
            CreatedBy        = "seed",
            CreationDate     = FixedNow.AddDays(-30),
            LastUpdateDate   = FixedNow.AddDays(-30)
        });
        await db.SaveChangesAsync();
        await SatisfyGateAsync(db, "AMB-001");

        var handler = BuildHandler(db);
        var result  = await handler.Handle(
            new EvaluateRankCommand("AMB-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RankAchieved.Should().BeTrue();
        result.Value.AchievedRank!.SortOrder.Should().Be(5);

        // Newly-created rows are the three intermediate ranks (3, 4, 5). They are stamped at
        // the evaluation instant with a monotonic +1s offset per rank, so filter by the
        // evaluation window rather than an exact-equals on FixedNow.
        var newRows = db.MemberRankHistories
            .Include(h => h.RankDefinition)
            .Where(h => h.AchievedAt >= FixedNow)
            .OrderBy(h => h.RankDefinition!.SortOrder)
            .ToList();

        newRows.Should().HaveCount(3);
        newRows.Select(h => h.RankDefinitionId).Should().BeEquivalentTo(new[] { 3, 4, 5 });
        newRows[0].PreviousRankId.Should().Be(2);
        newRows[1].PreviousRankId.Should().Be(3);
        newRows[2].PreviousRankId.Should().Be(4);
    }

    [Fact]
    public async Task Handle_WhenSkippingMultipleRanks_StampsDistinctIncreasingAchievedAt()
    {
        // A member who jumps several ranks in ONE evaluation must NOT have every rank row
        // recorded at the identical instant — that produces physically impossible history
        // ("achieved two ranks at the exact same second"). Each successive rank's AchievedAt
        // must be strictly greater than the previous one, ordered by SortOrder.
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        await db.RankDefinitions.AddRangeAsync(
            BuildRank(1, sortOrder: 1),
            BuildRank(2, sortOrder: 2),
            BuildRank(3, sortOrder: 3));
        await db.SaveChangesAsync();
        await SatisfyGateAsync(db, "AMB-001");

        var handler = BuildHandler(db);
        var result  = await handler.Handle(new EvaluateRankCommand("AMB-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var rows = db.MemberRankHistories
            .Include(h => h.RankDefinition)
            .OrderBy(h => h.RankDefinition!.SortOrder)
            .ToList();

        rows.Should().HaveCount(3);

        // No two ranks share an AchievedAt …
        rows.Select(h => h.AchievedAt).Distinct().Should().HaveCount(3,
            "each rank in a multi-rank climb must have its own distinct AchievedAt");

        // … and they strictly increase with SortOrder.
        rows[0].AchievedAt.Should().BeBefore(rows[1].AchievedAt);
        rows[1].AchievedAt.Should().BeBefore(rows[2].AchievedAt);

        // First rank lands on the evaluation instant; later ranks carry the monotonic offset.
        rows[0].AchievedAt.Should().Be(FixedNow);
    }

    [Fact]
    public async Task Handle_WhenRankAchieved_InvalidatesCache()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        await db.RankDefinitions.AddAsync(BuildRank(1, sortOrder: 1, personalPointsReq: 0));
        await db.SaveChangesAsync();
        await SatisfyGateAsync(db, "AMB-001");

        var cache   = BuildCache();
        var handler = new EvaluateRankHandler(
            db, BuildClock().Object, BuildUser().Object,
            BuildQualification(db), cache.Object,
            BuildJobs().Object);

        await handler.Handle(new EvaluateRankCommand("AMB-001"), CancellationToken.None);

        cache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
