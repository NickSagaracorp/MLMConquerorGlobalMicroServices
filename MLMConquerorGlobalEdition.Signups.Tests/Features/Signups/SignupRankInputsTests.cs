using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Jobs;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.CompleteSignup;
using MLMConquerorGlobalEdition.SignupAPI.Jobs;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Signups;

/// <summary>
/// Verifies the real signup pipeline (CompleteSignupHandler) populates the inputs the
/// rank engine consumes — EnrollmentPoints propagation up the enrollment upline and
/// membership activation. Deliberately lives in Signups.Tests (not RankEngine.Tests):
/// these tests construct the real CompleteSignupHandler and depend on SignupAPI internals,
/// and Signups.Tests already references SignupAPI and has the handler-test infrastructure.
/// </summary>
/// <remarks>
/// Integration-style tests that drive the real <see cref="CompleteSignupHandler"/> end-to-end
/// and verify it correctly populates the data the rank engine consumes:
///   - MemberStatisticEntity.PersonalPoints / EnrollmentPoints for the new member
///   - EnrollmentPoints propagation up the WHOLE enrollment upline
///   - MembershipSubscription.SubscriptionStatus becomes Active
/// </remarks>
public class SignupRankInputsTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 25, 12, 0, 0, DateTimeKind.Utc);

    // ── Mock factories (copied from CompleteSignupHandlerTests) ──────────────

    private static Mock<IDateTimeProvider> BuildDateTimeMock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static Mock<IJwtService> BuildJwtMock()
    {
        var m = new Mock<IJwtService>();
        m.Setup(j => j.GenerateAccessToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<string?>()))
         .Returns("mock-access-token");
        m.Setup(j => j.GenerateRefreshToken()).Returns("mock-refresh-token");
        m.Setup(j => j.AccessTokenExpiry).Returns(TimeSpan.FromMinutes(60));
        m.Setup(j => j.RefreshTokenExpiry).Returns(TimeSpan.FromDays(30));
        return m;
    }

    private static Mock<IS3FileService> BuildS3Mock()
    {
        var m = new Mock<IS3FileService>();
        m.Setup(s => s.UploadAsync(
                It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync("https://s3.example.com/screenshot.png");
        return m;
    }

    private static Mock<ISponsorBonusService> BuildSponsorBonusMock()
    {
        var m = new Mock<ISponsorBonusService>();
        m.Setup(s => s.ComputeAsync(
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
         .Returns(Task.CompletedTask);
        return m;
    }

    private static Mock<IFastStartBonusService> BuildFastStartBonusMock()
    {
        var m = new Mock<IFastStartBonusService>();
        m.Setup(s => s.ComputeAsync(
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .Returns(Task.CompletedTask);
        return m;
    }

    private static Mock<IEncryptionService> BuildEncryptionMock()
    {
        var m = new Mock<IEncryptionService>();
        m.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(p => "ENC:" + p);
        m.Setup(e => e.Decrypt(It.IsAny<string>()))
         .Returns<string>(c => c.StartsWith("ENC:") ? c[4..] : c);
        return m;
    }

    private static Mock<ITokenRedemptionService> BuildTokenRedemptionMock()
    {
        var m = new Mock<ITokenRedemptionService>();
        m.Setup(s => s.RedeemForSignupAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
         .ReturnsAsync(MLMConquerorGlobalEdition.SharedKernel.Result<bool>.Success(true));
        return m;
    }

    private static Mock<MLMConquerorGlobalEdition.Billing.Services.Recurring.IRecurringBillingEnrollmentService>
        BuildRecurringBillingEnrollmentMock()
    {
        var m = new Mock<MLMConquerorGlobalEdition.Billing.Services.Recurring.IRecurringBillingEnrollmentService>();
        m.Setup(s => s.EnsureStateForSubscriptionAsync(
                It.IsAny<MLMConquerorGlobalEdition.Domain.Entities.Membership.MembershipSubscription>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
         .Returns(Task.CompletedTask);
        return m;
    }

    // ── Entity builders ──────────────────────────────────────────────────────

    private static MemberProfile BuildMember(
        string memberId,
        string email,
        string? sponsorMemberId = null) => new()
    {
        MemberId       = memberId,
        Email          = email,
        FirstName      = "Test",
        LastName       = "User",
        DateOfBirth    = new DateTime(1990, 1, 1),
        MemberType     = MemberType.Ambassador,
        Status         = MemberAccountStatus.Pending,
        EnrollDate     = FixedNow,
        Country        = "US",
        SponsorMemberId = sponsorMemberId,
        CreatedBy      = email,
        CreationDate   = FixedNow,
        LastUpdateDate = FixedNow
    };

    private static Orders BuildPendingOrder(string orderId, string memberId, decimal total = 50) => new()
    {
        Id             = orderId,
        MemberId       = memberId,
        TotalAmount    = total,
        Status         = OrderStatus.Pending,
        OrderDate      = FixedNow,
        CreatedBy      = "user@example.com",
        CreationDate   = FixedNow,
        LastUpdateDate = FixedNow
    };

    private static MembershipSubscription BuildPendingSubscription(string id, string memberId) => new()
    {
        Id                 = id,
        MemberId           = memberId,
        MembershipLevelId  = 1,
        ChangeReason       = SubscriptionChangeReason.New,
        SubscriptionStatus = MembershipStatus.Pending,
        StartDate          = FixedNow,
        IsFree             = false,
        IsAutoRenew        = true,
        CreatedBy          = "user@example.com",
        CreationDate       = FixedNow,
        LastUpdateDate     = FixedNow
    };

    private static OrderDetail BuildOrderDetail(string orderId, string productId, decimal unitPrice) => new()
    {
        OrderId      = orderId,
        ProductId    = productId,
        Quantity     = 1,
        UnitPrice    = unitPrice,
        CreatedBy    = "user@example.com",
        CreationDate = FixedNow
    };

    /// <summary>
    /// Builds a Product with a known QualificationPoins value.
    /// Products are seeded as IsActive = true (real catalog), and each test
    /// also seeds an OrderDetail row for this product so the handler's
    /// productsExistInCatalog guard evaluates (true &amp;&amp; !false) = false —
    /// the NO_PRODUCTS_SELECTED early-return is never triggered, faithfully
    /// matching a real signup where the order already has product line items.
    /// </summary>
    private static Product BuildProduct(string id, int qualPoints) => new()
    {
        Id             = id,
        Name           = $"Pack {id}",
        Description    = "Test pack",
        ImageUrl       = "https://cdn.example.com/pack.png",
        MonthlyFee     = 50,
        SetupFee       = 0,
        QualificationPoins = qualPoints,
        IsActive       = true,
        IsDeleted      = false,
        CreatedBy      = "seed",
        CreationDate   = FixedNow,
        LastUpdateDate = FixedNow
    };

    private static GenealogyEntity BuildGenealogyNode(
        string memberId,
        string? parentMemberId,
        string hierarchyPath,
        int level) => new()
    {
        MemberId        = memberId,
        ParentMemberId  = parentMemberId,
        HierarchyPath   = hierarchyPath,
        Level           = level,
        CreatedBy       = "seed",
        CreationDate    = FixedNow,
        LastUpdateDate  = FixedNow
    };

    private static MemberStatisticEntity BuildMemberStatistic(
        string memberId,
        int enrollmentPoints,
        int personalPoints = 0) => new()
    {
        MemberId         = memberId,
        PersonalPoints   = personalPoints,
        EnrollmentPoints = enrollmentPoints,
        CreatedBy        = "seed",
        CreationDate     = FixedNow
    };

    private static ApplicationUser BuildInactiveUser(string memberId, string email) => new()
    {
        Id                 = Guid.NewGuid().ToString(),
        UserName           = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email              = email,
        NormalizedEmail    = email.ToUpperInvariant(),
        EmailConfirmed     = false,
        MemberProfileId    = memberId,
        IsActive           = false,
        CreationDate       = FixedNow,
        CreatedBy          = email
    };

    private static CompleteSignupRequest BuildRequest() => new()
    {
        PaymentMethod = PaymentMethodType.DiscountCode,
        DiscountCode  = "FREE100"
    };

    private CompleteSignupHandler BuildHandler(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db,
        Mock<UserManager<ApplicationUser>> userMgr)
    {
        return new CompleteSignupHandler(
            db,
            BuildDateTimeMock().Object,
            BuildS3Mock().Object,
            BuildSponsorBonusMock().Object,
            BuildFastStartBonusMock().Object,
            userMgr.Object,
            BuildJwtMock().Object,
            BuildEncryptionMock().Object,
            BuildTokenRedemptionMock().Object,
            BuildRecurringBillingEnrollmentMock().Object);
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that CompleteSignupHandler propagates EnrollmentPoints up the
    /// ENTIRE enrollment upline, not just to the direct sponsor.
    ///
    /// Chain: G (grandparent) → S (sponsor) → N (new member)
    ///
    /// The handler reads the SPONSOR's HierarchyPath ("/G/S/") and parses it
    /// into ancestor IDs ["G", "S"]. Both G and S therefore receive
    /// EnrollmentPoints += totalQualPoints.
    /// </summary>
    [Fact]
    public async Task CompleteSignup_PropagatesEnrollmentPointsUpTheWholeUpline()
    {
        await using var db = InMemoryDbHelper.Create();

        const string gId    = "AMB-G01";
        const string sId    = "AMB-S01";
        const string nId    = "AMB-N01";
        const string nEmail = "new-upline@example.com";
        const string orderId = "ORD-UPLINE-01";
        const string subId   = "SUB-UPLINE-01";

        const int gStartPoints = 100;
        const int sStartPoints = 200;
        const int qualPoints   = 50;  // product QualificationPoins

        // G — grandparent: no upline itself
        // HierarchyPath must contain the exact MemberId strings so ParseHierarchyPath matches
        var gMember   = BuildMember(gId, "g@example.com");
        gMember.Status = MemberAccountStatus.Active;  // already active (not the new signup)
        var gGeneNode = BuildGenealogyNode(gId, null, $"/{gId}/", 1);
        var gStats    = BuildMemberStatistic(gId, gStartPoints);

        // S — sponsor: parent is G
        var sMember   = BuildMember(sId, "s@example.com", sponsorMemberId: gId);
        sMember.Status = MemberAccountStatus.Active;
        var sGeneNode = BuildGenealogyNode(sId, gId, $"/{gId}/{sId}/", 2);
        var sStats    = BuildMemberStatistic(sId, sStartPoints);

        // N — new member: sponsor is S; in Pending state
        var nMember      = BuildMember(nId, nEmail, sponsorMemberId: sId);
        var nGeneNode    = BuildGenealogyNode(nId, sId, $"/{gId}/{sId}/{nId}/", 3);
        var nOrder       = BuildPendingOrder(orderId, nId);
        var nSub         = BuildPendingSubscription(subId, nId);
        var product      = BuildProduct("P-RANK-01", qualPoints);
        var nAppUser     = BuildInactiveUser(nId, nEmail);

        await db.MemberProfiles.AddRangeAsync(gMember, sMember, nMember);
        await db.GenealogyTree.AddRangeAsync(gGeneNode, sGeneNode, nGeneNode);
        await db.MemberStatistics.AddRangeAsync(gStats, sStats);
        await db.Products.AddAsync(product);
        await db.Orders.AddAsync(nOrder);
        await db.OrderDetails.AddAsync(BuildOrderDetail(orderId, product.Id, 50));
        await db.MembershipSubscriptions.AddAsync(nSub);
        await db.SaveChangesAsync();

        var userMgr = UserManagerHelper.Create();
        userMgr.Setup(u => u.FindByEmailAsync(nEmail)).ReturnsAsync(nAppUser);
        userMgr.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>()))
               .ReturnsAsync(IdentityResult.Success);

        var handler = BuildHandler(db, userMgr);

        // Act
        var result = await handler.Handle(
            new CompleteSignupCommand(orderId, BuildRequest()), CancellationToken.None);

        // Sprint-16 — ancestor propagation is now eventual-consistency. The handler
        // enqueues MemberStatisticDelta rows and ApplyMemberStatisticDeltasJob rolls
        // them up on its cadence. To assert the rank-engine input contract is still
        // honoured end-to-end, we drain the queue inline.
        var applyJob = new ApplyMemberStatisticDeltasJob(
            db, BuildDateTimeMock().Object, NullLogger<ApplyMemberStatisticDeltasJob>.Instance);
        await applyJob.ExecuteAsync(CancellationToken.None);

        // Assert — handler succeeded
        result.IsSuccess.Should().BeTrue(
            because: "all required data is present and the signup is valid");

        // N's own stats are seeded INLINE (no race because the new member doesn't exist anywhere else)
        var nStats = await db.MemberStatistics.FirstOrDefaultAsync(s => s.MemberId == nId);
        nStats.Should().NotBeNull(because: "CompleteSignupHandler must create a stat row for the new member");
        nStats!.PersonalPoints.Should().Be(qualPoints,
            because: "PersonalPoints is set to totalQualPoints (sum of OrderDetail product points)");
        nStats.EnrollmentPoints.Should().Be(qualPoints,
            because: "a new leaf member's EnrollmentPoints seed equals its own PersonalPoints");

        // S (direct sponsor) gets +qualPoints AFTER the delta-apply job runs.
        // The handler loads the SPONSOR'S GenealogyEntity (sGeneNode) whose
        // HierarchyPath="/{gId}/{sId}/", which ParseHierarchyPath splits into [gId, sId].
        // Both G and S are therefore in the ancestor list — both got a delta row.
        var sStatsAfter = await db.MemberStatistics.FirstOrDefaultAsync(s => s.MemberId == sId);
        sStatsAfter.Should().NotBeNull();
        sStatsAfter!.EnrollmentPoints.Should().Be(sStartPoints + qualPoints,
            because: "the direct sponsor S must have its EnrollmentPoints incremented by N's qualification points (post-drain)");

        // G (grandparent) also gets +qualPoints — the WHOLE upline is walked, not just the direct sponsor
        var gStatsAfter = await db.MemberStatistics.FirstOrDefaultAsync(s => s.MemberId == gId);
        gStatsAfter.Should().NotBeNull();
        gStatsAfter!.EnrollmentPoints.Should().Be(gStartPoints + qualPoints,
            because: "G is in S's HierarchyPath so the ENTIRE upline receives the propagation, " +
                     "which is the critical rank-engine input this test protects");
    }

    /// <summary>
    /// Verifies that CompleteSignupHandler sets the membership subscription to Active.
    /// Active status is a prerequisite for the rank engine to count Personal Customer Points.
    /// </summary>
    [Fact]
    public async Task CompleteSignup_ActivatesMembershipSubscription()
    {
        await using var db = InMemoryDbHelper.Create();

        const string memberId = "AMB-SUB-01";
        const string email    = "sub-activate@example.com";
        const string orderId  = "ORD-SUB-01";
        const string subId    = "SUB-ACTIVATE-01";

        // Single member with no upline — subscription activation test only
        var member  = BuildMember(memberId, email);
        var order   = BuildPendingOrder(orderId, memberId);
        var sub     = BuildPendingSubscription(subId, memberId);
        var product = BuildProduct("P-SUB-01", 30);
        var appUser = BuildInactiveUser(memberId, email);

        // Genealogy node with no ancestors (HierarchyPath contains only this member's id)
        var geneNode = BuildGenealogyNode(memberId, null, $"/{memberId}/", 1);

        await db.MemberProfiles.AddAsync(member);
        await db.GenealogyTree.AddAsync(geneNode);
        await db.Products.AddAsync(product);
        await db.Orders.AddAsync(order);
        await db.OrderDetails.AddAsync(BuildOrderDetail(orderId, product.Id, 50));
        await db.MembershipSubscriptions.AddAsync(sub);
        await db.SaveChangesAsync();

        var userMgr = UserManagerHelper.Create();
        userMgr.Setup(u => u.FindByEmailAsync(email)).ReturnsAsync(appUser);
        userMgr.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>()))
               .ReturnsAsync(IdentityResult.Success);

        var handler = BuildHandler(db, userMgr);

        // Act
        var result = await handler.Handle(
            new CompleteSignupCommand(orderId, BuildRequest()), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var updatedSub = await db.MembershipSubscriptions
            .FirstOrDefaultAsync(s => s.MemberId == memberId);
        updatedSub.Should().NotBeNull();
        updatedSub!.SubscriptionStatus.Should().Be(MembershipStatus.Active,
            because: "the rank engine only counts members with Active subscriptions toward Personal Customer Points");
    }

    /// <summary>
    /// Verifies that a new member with no sponsor (SponsorMemberId == null) still gets
    /// its own MemberStatisticEntity seeded, and that no upline rows are created or
    /// modified (there is no upline to propagate to).
    /// </summary>
    [Fact]
    public async Task CompleteSignup_NewMemberWithNoSponsor_SeedsOwnStatsWithoutPropagation()
    {
        await using var db = InMemoryDbHelper.Create();

        const string memberId = "AMB-SOLO-01";
        const string email    = "solo@example.com";
        const string orderId  = "ORD-SOLO-01";
        const string subId    = "SUB-SOLO-01";
        const int    qualPoints = 75;

        var member  = BuildMember(memberId, email, sponsorMemberId: null); // no sponsor
        var order   = BuildPendingOrder(orderId, memberId);
        var sub     = BuildPendingSubscription(subId, memberId);
        var product = BuildProduct("P-SOLO-01", qualPoints);
        var appUser = BuildInactiveUser(memberId, email);

        // No GenealogyEntity seeded: with SponsorMemberId == null the handler never
        // walks an ancestor path, so the member needs no enrollment-tree node here.
        await db.MemberProfiles.AddAsync(member);
        await db.Products.AddAsync(product);
        await db.Orders.AddAsync(order);
        await db.OrderDetails.AddAsync(BuildOrderDetail(orderId, product.Id, 80));
        await db.MembershipSubscriptions.AddAsync(sub);
        await db.SaveChangesAsync();

        var userMgr = UserManagerHelper.Create();
        userMgr.Setup(u => u.FindByEmailAsync(email)).ReturnsAsync(appUser);
        userMgr.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>()))
               .ReturnsAsync(IdentityResult.Success);

        var handler = BuildHandler(db, userMgr);

        // Act
        var result = await handler.Handle(
            new CompleteSignupCommand(orderId, BuildRequest()), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Own stats seeded
        var stats = await db.MemberStatistics.FirstOrDefaultAsync(s => s.MemberId == memberId);
        stats.Should().NotBeNull(because: "CompleteSignupHandler must create a stat row for any new member");
        stats!.EnrollmentPoints.Should().Be(qualPoints,
            because: "with no upline, the new member's EnrollmentPoints equals its own QualificationPoins");
        stats.PersonalPoints.Should().Be(qualPoints,
            because: "PersonalPoints is always seeded from totalQualPoints");

        // No other stat rows were created (no upline was touched)
        var allStats = await db.MemberStatistics.ToListAsync();
        allStats.Should().HaveCount(1,
            because: "with no sponsor, no upline propagation occurs so only the new member's own stat row exists");
    }
}
