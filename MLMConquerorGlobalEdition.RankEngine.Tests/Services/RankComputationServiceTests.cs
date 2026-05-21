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

public class RankComputationServiceTests
{
    private static readonly DateTime Now = new(2026, 5, 21, 0, 0, 0, DateTimeKind.Utc);

    private static RankComputationService Build(AppDbContext db)
    {
        var et = new EnrollmentTeamPointsService(db);
        var pcp = new PersonalCustomerPointsService(db);
        return new RankComputationService(db, new RankQualificationService(db, et, pcp));
    }

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
    public async Task GetSummary_WhenGateFails_CurrentRankIsNull()
    {
        await using var db = InMemoryDbHelper.Create();
        await RankGateSeeder.SeedAsync(db, NullLogger.Instance);
        db.MemberProfiles.Add(new MemberProfile { MemberId = "M", FirstName = "T", LastName = "U",
            Email = "m@x.com", MemberType = MemberType.Ambassador, Country = "US", EnrollDate = Now,
            CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now });
        db.RankDefinitions.Add(new RankDefinition { Id = 1, Name = "Silver", SortOrder = 1,
            Status = RankDefinitionStatus.Active, CreatedBy = "seed", CreationDate = Now,
            Requirements = new List<RankRequirement> { new() { Id = 100, RankDefinitionId = 1,
                LevelNo = 0, EnrollmentTeam = 0, TeamPoints = 0, CreatedBy = "seed", CreationDate = Now } } });
        await db.SaveChangesAsync();

        var summary = await Build(db).GetSummaryAsync("M");

        // Rank 1 has zero point requirements, but the member has no PCP => gate fails.
        summary.CurrentRankId.Should().BeNull();
    }

    [Fact]
    public async Task GetSummary_WhenGatePassesAndThresholdsMet_CurrentRankIsSet()
    {
        await using var db = InMemoryDbHelper.Create();
        await RankGateSeeder.SeedAsync(db, NullLogger.Instance);
        db.MemberProfiles.Add(new MemberProfile { MemberId = "M", FirstName = "T", LastName = "U",
            Email = "m@x.com", MemberType = MemberType.Ambassador, Country = "US", EnrollDate = Now,
            CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now });
        // Active membership gives M 12 PCP => gate passes (pcp >= 12, no sponsored members needed).
        await ActiveMembershipAsync(db, "M", 12);
        db.RankDefinitions.Add(new RankDefinition { Id = 1, Name = "Silver", SortOrder = 1,
            Status = RankDefinitionStatus.Active, CreatedBy = "seed", CreationDate = Now,
            Requirements = new List<RankRequirement> { new() { Id = 100, RankDefinitionId = 1,
                LevelNo = 0, EnrollmentTeam = 0, TeamPoints = 0, SponsoredMembers = 0,
                ExternalMembers = 0, PersonalPoints = 0, SalesVolume = 0,
                CreatedBy = "seed", CreationDate = Now } } });
        await db.SaveChangesAsync();

        var summary = await Build(db).GetSummaryAsync("M");

        summary.CurrentRankId.Should().Be(1);
        summary.CurrentRankName.Should().Be("Silver");
    }
}
