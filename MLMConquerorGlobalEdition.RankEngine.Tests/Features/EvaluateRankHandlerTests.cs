using MediatR;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.RankEngine.Features.EvaluateRank;
using MLMConquerorGlobalEdition.RankEngine.Features.GenerateCertificate;
using MLMConquerorGlobalEdition.RankEngine.Features.GetRankProgress;
using MLMConquerorGlobalEdition.RankEngine.Services;
using MLMConquerorGlobalEdition.RankEngine.Tests.Helpers;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IEmailService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IEmailService;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;

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

    private static Mock<IPushNotificationService> BuildPush()
    {
        var m = new Mock<IPushNotificationService>();
        m.Setup(p => p.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return m;
    }

    private static Mock<IEmailService> BuildEmail()
    {
        var m = new Mock<IEmailService>();
        m.Setup(e => e.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return m;
    }

    private static Mock<ISender> BuildMediator()
    {
        var m = new Mock<ISender>();
        m.Setup(s => s.Send(
                It.IsAny<GenerateCertificateCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SharedKernel.Result<DTOs.CertificateGenerationResponse>.Success(
                new DTOs.CertificateGenerationResponse()));
        return m;
    }

    private EvaluateRankHandler BuildHandler(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db)
    {
        var progressHelper = new GetRankProgressHandler(db, BuildClock().Object);
        return new EvaluateRankHandler(
            db,
            BuildClock().Object,
            BuildUser().Object,
            progressHelper,
            BuildCache().Object,
            BuildPush().Object,
            BuildEmail().Object,
            BuildMediator().Object);
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

        var handler = BuildHandler(db);
        var result  = await handler.Handle(
            new EvaluateRankCommand("AMB-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RankAchieved.Should().BeTrue();
        // Should achieve rank 2 (highest qualifying)
        result.Value.AchievedRank!.SortOrder.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WhenRankAchieved_InvalidatesCache()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        await db.RankDefinitions.AddAsync(BuildRank(1, sortOrder: 1, personalPointsReq: 0));
        await db.SaveChangesAsync();

        var cache   = BuildCache();
        var progressHelper = new GetRankProgressHandler(db, BuildClock().Object);
        var handler = new EvaluateRankHandler(
            db, BuildClock().Object, BuildUser().Object,
            progressHelper, cache.Object,
            BuildPush().Object, BuildEmail().Object, BuildMediator().Object);

        await handler.Handle(new EvaluateRankCommand("AMB-001"), CancellationToken.None);

        cache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
