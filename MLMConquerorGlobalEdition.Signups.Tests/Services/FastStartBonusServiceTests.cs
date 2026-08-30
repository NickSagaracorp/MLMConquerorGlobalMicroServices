using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Services;

public class FastStartBonusServiceTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static MemberProfile BuildMember(
        string memberId, string? sponsor, Guid userId, DateTime? enrollDate = null) => new()
    {
        MemberId        = memberId,
        UserId          = userId,
        FirstName       = "Test",
        LastName        = "User",
        MemberType      = MemberType.Ambassador,
        EnrollDate      = enrollDate ?? FixedNow.AddMonths(-1),
        Country         = "US",
        SponsorMemberId = sponsor,
        CreatedBy       = "seed",
        LastUpdateDate  = FixedNow
    };

    private static Product BuildProduct(string id, int membershipLevelId) => new()
    {
        Id                 = id,
        Name               = "Test Product",
        Description        = "Test",
        ImageUrl           = string.Empty,
        MonthlyFee         = 99m,
        SetupFee           = 0m,
        MembershipLevelId  = membershipLevelId,
        QualificationPoins = 6,
        IsActive           = true,
        CreatedBy          = "seed",
        LastUpdateDate     = FixedNow
    };

    /// <summary>
    /// Seeds a product + order-detail row and immediately saves, so callers can control
    /// the exact insertion order of the order's two membership-linked products.
    /// </summary>
    private static async Task SeedProductAndOrderDetail(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db,
        string orderId, string productId, int membershipLevelId)
    {
        await db.Products.AddAsync(BuildProduct(productId, membershipLevelId));
        await db.OrderDetails.AddAsync(new OrderDetail
        {
            OrderId   = orderId,
            ProductId = productId,
            Quantity  = 1,
            UnitPrice = 99m,
            CreatedBy = "seed"
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds everything ComputeAsync needs to reach the award path EXCEPT the new
    /// member's own order/products: the sponsor (with a FSB countdown whose Window 1
    /// covers <see cref="FixedNow"/>), the FSB commission type, and one already-committed
    /// Elite/Turbo downline (<c>AMB-FIRST</c>) enrolled inside that window — which is what
    /// makes the member under test the "+1" that completes the pair and fires the bonus.
    /// </summary>
    private static async Task<Guid> SeedFsbScaffolding(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db)
    {
        var sponsorUserId = Guid.NewGuid();
        var sponsor = BuildMember("AMB-SPONSOR", sponsor: null, userId: sponsorUserId);
        var firstSponsored = BuildMember(
            "AMB-FIRST", sponsor: "AMB-SPONSOR", userId: Guid.NewGuid(),
            enrollDate: FixedNow.AddDays(-1));

        await db.MemberProfiles.AddRangeAsync(sponsor, firstSponsored);

        await db.MembershipSubscriptions.AddAsync(new MembershipSubscription
        {
            MemberId           = "AMB-FIRST",
            MembershipLevelId  = 3, // Elite — eligible for FSB counting
            SubscriptionStatus = MembershipStatus.Active,
            ChangeReason       = SubscriptionChangeReason.New,
            StartDate          = FixedNow.AddDays(-1),
            CreatedBy          = "seed",
            LastUpdateDate     = FixedNow
        });

        await db.CommissionCountDowns.AddAsync(new MemberCommissionCountDown
        {
            MemberId                     = sponsorUserId,
            Member                       = sponsor,
            FastStartBonus1Start         = FixedNow.AddDays(-3),
            FastStartBonus1End           = FixedNow.AddDays(11),
            FastStartBonus1ExtendedStart = FixedNow.AddDays(-3),
            FastStartBonus1ExtendedEnd   = FixedNow.AddDays(11),
            FastStartBonus2Start         = FixedNow.AddDays(12),
            FastStartBonus2End           = FixedNow.AddDays(19),
            FastStartBonus3Start         = FixedNow.AddDays(20),
            FastStartBonus3End           = FixedNow.AddDays(27),
            CreatedBy                    = "seed",
            CreationDate                 = FixedNow,
            LastUpdateDate               = FixedNow
        });

        await db.CommissionTypes.AddAsync(new CommissionType
        {
            Id               = 50,
            Name             = "FSB-W1",
            IsActive         = true,
            IsPaidOnSignup   = true,
            IsSponsorBonus   = false,
            TriggerOrder     = 1,
            Amount           = 300m, // ComputeAsync pays half up-front: 150
            Percentage       = 0,
            PaymentDelayDays = 0,
            CreatedBy        = "seed",
            CreationDate     = FixedNow
        });

        await db.SaveChangesAsync();
        return sponsorUserId;
    }

    // ── Regression: the new member's order can carry BOTH the default Lifestyle
    // Ambassador product (level 1, always present per the catalog design) AND an
    // upgraded membership product (Elite/Turbo). FSB eligibility must always be
    // decided from the HIGHEST level on the order, regardless of which product
    // row the engine returns first — tested in both insertion orders, because the
    // original bug (FirstOrDefaultAsync, no ORDER BY) depended on exactly that.
    [Fact]
    public async Task ComputeAsync_WhenOrderHasLifestylePlusElite_LifestyleInsertedFirst_CreatesFsbEarning()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedFsbScaffolding(db);
        await db.MemberProfiles.AddAsync(
            BuildMember("AMB-NEW", sponsor: "AMB-SPONSOR", userId: Guid.NewGuid()));
        await db.SaveChangesAsync();

        // Lifestyle (level 1) inserted BEFORE Elite (level 3).
        await SeedProductAndOrderDetail(db, "ORD-NEW", "P-LIFESTYLE", membershipLevelId: 1);
        await SeedProductAndOrderDetail(db, "ORD-NEW", "P-ELITE", membershipLevelId: 3);

        var service = new FastStartBonusService(db);
        await service.ComputeAsync("AMB-SPONSOR", "AMB-NEW", "ORD-NEW", FixedNow, "seed", CancellationToken.None);
        await db.SaveChangesAsync();

        var earning = db.CommissionEarnings.Single();
        earning.BeneficiaryMemberId.Should().Be("AMB-SPONSOR");
        earning.CommissionTypeId.Should().Be(50);
        earning.Amount.Should().Be(150m); // 300 / 2
    }

    [Fact]
    public async Task ComputeAsync_WhenOrderHasLifestylePlusElite_EliteInsertedFirst_CreatesFsbEarning()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedFsbScaffolding(db);
        await db.MemberProfiles.AddAsync(
            BuildMember("AMB-NEW", sponsor: "AMB-SPONSOR", userId: Guid.NewGuid()));
        await db.SaveChangesAsync();

        // Elite (level 3) inserted BEFORE Lifestyle (level 1) — reversed insertion order.
        await SeedProductAndOrderDetail(db, "ORD-NEW", "P-ELITE", membershipLevelId: 3);
        await SeedProductAndOrderDetail(db, "ORD-NEW", "P-LIFESTYLE", membershipLevelId: 1);

        var service = new FastStartBonusService(db);
        await service.ComputeAsync("AMB-SPONSOR", "AMB-NEW", "ORD-NEW", FixedNow, "seed", CancellationToken.None);
        await db.SaveChangesAsync();

        var earning = db.CommissionEarnings.Single();
        earning.BeneficiaryMemberId.Should().Be("AMB-SPONSOR");
        earning.CommissionTypeId.Should().Be(50);
        earning.Amount.Should().Be(150m); // 300 / 2
    }

    [Fact]
    public async Task ComputeAsync_WhenOrderIsLifestyleOnly_DoesNotCreateFsbEarning()
    {
        // Correct behavior for the single-product case: Lifestyle Ambassador (level 1)
        // alone never triggers FSB — only Elite/Turbo does.
        await using var db = InMemoryDbHelper.Create();
        await SeedFsbScaffolding(db);
        await db.MemberProfiles.AddAsync(
            BuildMember("AMB-NEW", sponsor: "AMB-SPONSOR", userId: Guid.NewGuid()));
        await db.SaveChangesAsync();

        await SeedProductAndOrderDetail(db, "ORD-NEW", "P-LIFESTYLE", membershipLevelId: 1);

        var service = new FastStartBonusService(db);
        await service.ComputeAsync("AMB-SPONSOR", "AMB-NEW", "ORD-NEW", FixedNow, "seed", CancellationToken.None);
        await db.SaveChangesAsync();

        db.CommissionEarnings.Should().BeEmpty();
    }
}
