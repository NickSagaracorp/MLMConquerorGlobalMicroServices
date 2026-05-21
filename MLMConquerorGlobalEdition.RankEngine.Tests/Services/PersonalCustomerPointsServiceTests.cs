using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.RankEngine.Tests.Helpers;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Services;

public class PersonalCustomerPointsServiceTests
{
    private static readonly DateTime Now = new(2026, 5, 21, 0, 0, 0, DateTimeKind.Utc);

    private static MemberProfile Member(string id, string? sponsorId = null) => new()
    {
        MemberId = id, SponsorMemberId = sponsorId, FirstName = "T", LastName = "U",
        Email = $"{id}@x.com", MemberType = MemberType.Ambassador, Country = "US",
        EnrollDate = Now, CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now
    };

    private static async Task GiveMembershipAsync(
        Repository.Context.AppDbContext db, string memberId, MembershipStatus status, params int[] productPoints)
    {
        var orderId = $"ORD-{memberId}";
        db.Orders.Add(new Orders
        {
            Id = orderId, MemberId = memberId, Status = OrderStatus.Completed,
            OrderDate = Now, TotalAmount = 0,
            CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now
        });
        for (var i = 0; i < productPoints.Length; i++)
        {
            var productId = $"PRD-{memberId}-{i}";
            db.Products.Add(new Product
            {
                Id = productId, Name = $"P{i}", Description = "d", ImageUrl = "x",
                MonthlyFee = 0, SetupFee = 0, QualificationPoins = productPoints[i],
                CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now
            });
            db.OrderDetails.Add(new OrderDetail
            {
                OrderId = orderId, ProductId = productId, Quantity = 1, UnitPrice = 0,
                CreatedBy = "seed", CreationDate = Now
            });
        }
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            MemberId = memberId, MembershipLevelId = 1, SubscriptionStatus = status,
            StartDate = Now, LastOrderId = orderId, CreatedBy = "seed",
            CreationDate = Now, LastUpdateDate = Now
        });
        await db.SaveChangesAsync();
    }

    // ── GetMembershipPointsAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetMembershipPoints_SumsAllProductsOnTheActiveOrder()
    {
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(Member("M"));
        await GiveMembershipAsync(db, "M", MembershipStatus.Active, 5, 3); // membership + add-on
        var svc = new PersonalCustomerPointsService(db);

        (await svc.GetMembershipPointsAsync("M")).Should().Be(8);
    }

    [Fact]
    public async Task GetMembershipPoints_WhenMembershipNotActive_ReturnsZero()
    {
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(Member("M"));
        await GiveMembershipAsync(db, "M", MembershipStatus.Cancelled, 5, 3);
        var svc = new PersonalCustomerPointsService(db);

        (await svc.GetMembershipPointsAsync("M")).Should().Be(0);
    }

    // ── GetPersonalCustomerPointsAsync ─────────────────────────────────────

    [Fact]
    public async Task GetPersonalCustomerPoints_AddsOwnPlusActiveSponsored_IgnoresInactiveSponsored()
    {
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.AddRange(
            Member("M"), Member("S1", "M"), Member("S2", "M"), Member("S3", "M"));
        await db.SaveChangesAsync();
        await GiveMembershipAsync(db, "M",  MembershipStatus.Active, 4);     // own = 4
        await GiveMembershipAsync(db, "S1", MembershipStatus.Active, 3);     // +3
        await GiveMembershipAsync(db, "S2", MembershipStatus.Active, 3);     // +3
        await GiveMembershipAsync(db, "S3", MembershipStatus.Cancelled, 9);  // ignored
        var svc = new PersonalCustomerPointsService(db);

        (await svc.GetPersonalCustomerPointsAsync("M")).Should().Be(10);
    }

    [Fact]
    public async Task GetPersonalCustomerPoints_WhenNoMembershipAndNoSponsored_ReturnsZero()
    {
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(Member("LONE"));
        await db.SaveChangesAsync();
        var svc = new PersonalCustomerPointsService(db);

        (await svc.GetPersonalCustomerPointsAsync("LONE")).Should().Be(0);
    }

    [Fact]
    public async Task GetPersonalCustomerPoints_ExcludesIndirectGrandchildMembers()
    {
        // M sponsors S1 directly; S1 sponsors G1 (grandchild of M — NOT direct).
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.AddRange(
            Member("M"),
            Member("S1", "M"),   // direct child of M
            Member("G1", "S1")); // grandchild — SponsorMemberId == "S1", NOT "M"
        await db.SaveChangesAsync();
        await GiveMembershipAsync(db, "M",  MembershipStatus.Active, 4);  // own = 4
        await GiveMembershipAsync(db, "S1", MembershipStatus.Active, 3);  // direct sponsored = 3
        await GiveMembershipAsync(db, "G1", MembershipStatus.Active, 99); // must be excluded

        var svc = new PersonalCustomerPointsService(db);

        // Expected: 4 (M) + 3 (S1) = 7. G1's 99 must not be included.
        (await svc.GetPersonalCustomerPointsAsync("M")).Should().Be(7);
    }
}
