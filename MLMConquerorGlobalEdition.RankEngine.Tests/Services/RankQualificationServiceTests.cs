using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.RankEngine.Tests.Helpers;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Seeders;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Services;

public class RankQualificationServiceTests
{
    private static readonly DateTime Now = new(2026, 5, 21, 0, 0, 0, DateTimeKind.Utc);

    private static RankQualificationService Build(AppDbContext db) =>
        new(db, new EnrollmentTeamPointsService(db), new PersonalCustomerPointsService(db));

    private static MemberProfile Member(string id, string? sponsorId = null) => new()
    {
        MemberId = id, SponsorMemberId = sponsorId, FirstName = "T", LastName = "U",
        Email = $"{id}@x.com", MemberType = MemberType.Ambassador, Country = "US",
        EnrollDate = Now, CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now
    };

    private static async Task ActiveMembershipAsync(AppDbContext db, string memberId, int points)
    {
        var orderId = $"ORD-{memberId}";
        db.Orders.Add(new Orders { Id = orderId, MemberId = memberId, Status = OrderStatus.Completed,
            OrderDate = Now, CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now });
        var productId = $"PRD-{memberId}";
        db.Products.Add(new Product { Id = productId, Name = "P", Description = "d", ImageUrl = "x",
            MonthlyFee = 0, SetupFee = 0, QualificationPoins = points,
            CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now });
        db.OrderDetails.Add(new OrderDetail { OrderId = orderId, ProductId = productId, Quantity = 1,
            UnitPrice = 0, CreatedBy = "seed", CreationDate = Now });
        db.MembershipSubscriptions.Add(new MembershipSubscription { MemberId = memberId,
            MembershipLevelId = 1, SubscriptionStatus = MembershipStatus.Active, StartDate = Now,
            LastOrderId = orderId, CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task MeetsUniversalGate_TrueWhenTwelvePcpAndNoSponsored()
    {
        await using var db = InMemoryDbHelper.Create();
        await RankGateSeeder.SeedAsync(db, NullLogger.Instance);
        db.MemberProfiles.Add(Member("M"));
        await ActiveMembershipAsync(db, "M", 12);

        (await Build(db).MeetsUniversalGateAsync("M")).Should().BeTrue();
    }

    [Fact]
    public async Task MeetsUniversalGate_FalseWhenNinePcpAndTwoSponsored()
    {
        await using var db = InMemoryDbHelper.Create();
        await RankGateSeeder.SeedAsync(db, NullLogger.Instance);
        db.MemberProfiles.AddRange(Member("M"), Member("S1", "M"), Member("S2", "M"));
        await db.SaveChangesAsync();
        await ActiveMembershipAsync(db, "M", 9);   // own 9, 2 sponsored (no membership points)

        // 9 PCP < 12, and only 2 sponsored < 3 => gate fails.
        (await Build(db).MeetsUniversalGateAsync("M")).Should().BeFalse();
    }

    [Fact]
    public async Task MeetsUniversalGate_TrueWhenNinePcpAndThreeSponsored()
    {
        await using var db = InMemoryDbHelper.Create();
        await RankGateSeeder.SeedAsync(db, NullLogger.Instance);
        db.MemberProfiles.AddRange(
            Member("M"), Member("S1", "M"), Member("S2", "M"), Member("S3", "M"));
        await db.SaveChangesAsync();
        await ActiveMembershipAsync(db, "M", 9);

        // S1/S2/S3 have no Active membership => they contribute 0 PCP; the member's own
        // PCP stays 9. Gate passes the left branch: sponsored 3 >= 3 AND pcp 9 >= 9.
        (await Build(db).MeetsUniversalGateAsync("M")).Should().BeTrue();
    }

    [Fact]
    public async Task QualifiesForRank_FalseWhenGateFails_EvenIfPointsMet()
    {
        await using var db = InMemoryDbHelper.Create();
        await RankGateSeeder.SeedAsync(db, NullLogger.Instance);
        db.MemberProfiles.Add(Member("M"));
        await ActiveMembershipAsync(db, "M", 0);                    // 0 PCP => gate fails
        db.DualTeamTree.Add(new DualTeamEntity { MemberId = "M", LeftLegPoints = 1000,
            RightLegPoints = 1000, HierarchyPath = "/M/", CreatedBy = "seed",
            CreationDate = Now, LastUpdateDate = Now });
        await db.SaveChangesAsync();

        var req = new RankRequirement { TeamPoints = 350, MaxTeamPointsPerBranch = 0.5 };
        var result = await Build(db).QualifiesForRankAsync("M", req);

        result.MeetsDualTeam.Should().BeTrue();
        result.MeetsGate.Should().BeFalse();
        result.Qualifies.Should().BeFalse();
    }

    [Fact]
    public async Task QualifiesForRank_TrueWhenGatePassesAndAllAxesMet()
    {
        // Member M has 12 PCP (active membership) => gate passes (pcp >= 12, no sponsored needed).
        // DualTeam: both legs = 200 each; req TeamPoints=350, MaxTeamPointsPerBranch=0.5
        //   perLegCap = round(0.5 * 350) = 175
        //   eligible = min(200,175) + min(200,175) = 175+175 = 350 => meets threshold.
        // EnrollmentTeam = 0 => ET axis opts out (MeetsEnrollmentTeam = true).
        await using var db = InMemoryDbHelper.Create();
        await RankGateSeeder.SeedAsync(db, NullLogger.Instance);
        db.MemberProfiles.Add(Member("M"));
        await ActiveMembershipAsync(db, "M", 12);   // 12 PCP => gate passes
        db.DualTeamTree.Add(new DualTeamEntity { MemberId = "M", LeftLegPoints = 200,
            RightLegPoints = 200, HierarchyPath = "/M/", CreatedBy = "seed",
            CreationDate = Now, LastUpdateDate = Now });
        await db.SaveChangesAsync();

        var req = new RankRequirement
        {
            TeamPoints = 350, MaxTeamPointsPerBranch = 0.5, EnrollmentTeam = 0,
            SponsoredMembers = 0, ExternalMembers = 0   // opt out axes with entity-default = 1
        };
        var result = await Build(db).QualifiesForRankAsync("M", req);

        result.MeetsGate.Should().BeTrue();
        result.MeetsDualTeam.Should().BeTrue();
        result.MeetsEnrollmentTeam.Should().BeTrue();  // ET = 0, axis opted out
        result.Qualifies.Should().BeTrue();
    }
}
