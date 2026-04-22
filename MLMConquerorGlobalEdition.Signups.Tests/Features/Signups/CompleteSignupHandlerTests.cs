using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.CompleteSignup;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Signups;

public class CompleteSignupHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 25, 12, 0, 0, DateTimeKind.Utc);

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
        m.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync("https://s3.example.com/screenshot.png");
        return m;
    }

    private static Mock<ISponsorBonusService> BuildSponsorBonusMock()
    {
        var m = new Mock<ISponsorBonusService>();
        m.Setup(s => s.ComputeAsync(
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
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

    private static MemberProfile BuildMember(string memberId, string email = "user@example.com") => new()
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
        CreatedBy      = email,
        CreationDate   = FixedNow,
        LastUpdateDate = FixedNow
    };

    private static Orders BuildPendingOrder(string orderId, string memberId, decimal total = 80) => new()
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

    private static CompleteSignupRequest BuildRequest(PaymentMethodType method = PaymentMethodType.DiscountCode) => new()
    {
        PaymentMethod = method,
        DiscountCode  = method == PaymentMethodType.DiscountCode ? "FREE100" : null
    };


    [Fact]
    public async Task Handle_WhenSignupNotFound_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var userMgr = UserManagerHelper.Create();
        userMgr.Setup(u => u.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        var handler = new CompleteSignupHandler(
            db, BuildDateTimeMock().Object, BuildS3Mock().Object,
            BuildSponsorBonusMock().Object, BuildFastStartBonusMock().Object, userMgr.Object, BuildJwtMock().Object);

        var result = await handler.Handle(
            new CompleteSignupCommand("NON-EXISTENT", BuildRequest()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SIGNUP_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenOrderIsNotPending_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var member = BuildMember("AMB-001");
        var order  = BuildPendingOrder("ORD-001", "AMB-001");
        order.Status = OrderStatus.Completed;

        await db.MemberProfiles.AddAsync(member);
        await db.Orders.AddAsync(order);
        await db.SaveChangesAsync();

        var userMgr = UserManagerHelper.Create();
        var handler = new CompleteSignupHandler(
            db, BuildDateTimeMock().Object, BuildS3Mock().Object,
            BuildSponsorBonusMock().Object, BuildFastStartBonusMock().Object, userMgr.Object, BuildJwtMock().Object);

        var result = await handler.Handle(
            new CompleteSignupCommand("ORD-001", BuildRequest()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SIGNUP_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenNoProductsSelected_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var member = BuildMember("AMB-002");
        var order  = BuildPendingOrder("ORD-002", "AMB-002"); // no OrderDetails

        // A product must exist in the catalog so the handler considers the catalog active
        await db.Products.AddAsync(new Product
        {
            Id             = "P-CATALOG",
            Name           = "Ambassador Pack",
            Description    = "Default pack",
            ImageUrl       = "https://cdn.example.com/pack.png",
            MonthlyFee     = 80,
            SetupFee       = 0,
            IsActive       = true,
            CreatedBy      = "seed",
            CreationDate   = FixedNow,
            LastUpdateDate = FixedNow
        });
        await db.MemberProfiles.AddAsync(member);
        await db.Orders.AddAsync(order);
        await db.SaveChangesAsync();

        var userMgr = UserManagerHelper.Create();
        var handler = new CompleteSignupHandler(
            db, BuildDateTimeMock().Object, BuildS3Mock().Object,
            BuildSponsorBonusMock().Object, BuildFastStartBonusMock().Object, userMgr.Object, BuildJwtMock().Object);

        var result = await handler.Handle(
            new CompleteSignupCommand("ORD-002", BuildRequest()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_PRODUCTS_SELECTED");
    }

    [Fact]
    public async Task Handle_WhenMemberNotFound_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        // Order references a member that doesn't exist
        var order = BuildPendingOrder("ORD-003", "AMB-GHOST");
        await db.Orders.AddAsync(order);
        await db.OrderDetails.AddAsync(BuildOrderDetail("ORD-003", "P-001", 80));
        await db.SaveChangesAsync();

        var userMgr = UserManagerHelper.Create();
        var handler = new CompleteSignupHandler(
            db, BuildDateTimeMock().Object, BuildS3Mock().Object,
            BuildSponsorBonusMock().Object, BuildFastStartBonusMock().Object, userMgr.Object, BuildJwtMock().Object);

        var result = await handler.Handle(
            new CompleteSignupCommand("ORD-003", BuildRequest()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MEMBER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenUserAlreadyActive_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var member       = BuildMember("AMB-004", "active@example.com");
        var order        = BuildPendingOrder("ORD-004", "AMB-004");
        var subscription = BuildPendingSubscription("SUB-004", "AMB-004");

        await db.MemberProfiles.AddAsync(member);
        await db.Orders.AddAsync(order);
        await db.OrderDetails.AddAsync(BuildOrderDetail("ORD-004", "P-001", 80));
        await db.MembershipSubscriptions.AddAsync(subscription);
        await db.SaveChangesAsync();

        var activeUser = BuildInactiveUser("AMB-004", "active@example.com");
        activeUser.IsActive = true; // already active

        var userMgr = UserManagerHelper.Create();
        userMgr.Setup(u => u.FindByEmailAsync("active@example.com")).ReturnsAsync(activeUser);

        var handler = new CompleteSignupHandler(
            db, BuildDateTimeMock().Object, BuildS3Mock().Object,
            BuildSponsorBonusMock().Object, BuildFastStartBonusMock().Object, userMgr.Object, BuildJwtMock().Object);

        var result = await handler.Handle(
            new CompleteSignupCommand("ORD-004", BuildRequest()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
    }


    [Fact]
    public async Task Handle_WhenValid_ActivatesOrderAndReturnsSuccess()
    {
        await using var db = InMemoryDbHelper.Create();
        const string memberId = "AMB-010";
        const string email    = "new@example.com";

        var member       = BuildMember(memberId, email);
        var order        = BuildPendingOrder("ORD-010", memberId);
        var subscription = BuildPendingSubscription("SUB-010", memberId);
        var appUser      = BuildInactiveUser(memberId, email);

        await db.MemberProfiles.AddAsync(member);
        await db.Orders.AddAsync(order);
        await db.OrderDetails.AddAsync(BuildOrderDetail("ORD-010", "P-001", 80));
        await db.MembershipSubscriptions.AddAsync(subscription);
        await db.SaveChangesAsync();

        var userMgr = UserManagerHelper.Create();
        userMgr.Setup(u => u.FindByEmailAsync(email)).ReturnsAsync(appUser);
        userMgr.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>()))
               .ReturnsAsync(IdentityResult.Success);

        var handler = new CompleteSignupHandler(
            db, BuildDateTimeMock().Object, BuildS3Mock().Object,
            BuildSponsorBonusMock().Object, BuildFastStartBonusMock().Object, userMgr.Object, BuildJwtMock().Object);

        var result = await handler.Handle(
            new CompleteSignupCommand("ORD-010", BuildRequest()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updatedOrder = await db.Orders.FindAsync("ORD-010");
        updatedOrder!.Status.Should().Be(OrderStatus.Completed);
    }

    [Fact]
    public async Task Handle_WhenValid_ActivatesMemberProfile()
    {
        await using var db = InMemoryDbHelper.Create();
        const string memberId = "AMB-011";
        const string email    = "amb011@example.com";

        var member       = BuildMember(memberId, email);
        var order        = BuildPendingOrder("ORD-011", memberId);
        var subscription = BuildPendingSubscription("SUB-011", memberId);
        var appUser      = BuildInactiveUser(memberId, email);

        await db.MemberProfiles.AddAsync(member);
        await db.Orders.AddAsync(order);
        await db.OrderDetails.AddAsync(BuildOrderDetail("ORD-011", "P-001", 80));
        await db.MembershipSubscriptions.AddAsync(subscription);
        await db.SaveChangesAsync();

        var userMgr = UserManagerHelper.Create();
        userMgr.Setup(u => u.FindByEmailAsync(email)).ReturnsAsync(appUser);
        userMgr.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>()))
               .ReturnsAsync(IdentityResult.Success);

        var handler = new CompleteSignupHandler(
            db, BuildDateTimeMock().Object, BuildS3Mock().Object,
            BuildSponsorBonusMock().Object, BuildFastStartBonusMock().Object, userMgr.Object, BuildJwtMock().Object);

        await handler.Handle(new CompleteSignupCommand("ORD-011", BuildRequest()), CancellationToken.None);

        var updatedMember = await db.MemberProfiles.FirstAsync(m => m.MemberId == memberId);
        updatedMember.Status.Should().Be(MemberAccountStatus.Active);
    }

    [Fact]
    public async Task Handle_WhenValid_ActivatesSubscription()
    {
        await using var db = InMemoryDbHelper.Create();
        const string memberId = "AMB-012";
        const string email    = "amb012@example.com";

        var member       = BuildMember(memberId, email);
        var order        = BuildPendingOrder("ORD-012", memberId);
        var subscription = BuildPendingSubscription("SUB-012", memberId);
        var appUser      = BuildInactiveUser(memberId, email);

        await db.MemberProfiles.AddAsync(member);
        await db.Orders.AddAsync(order);
        await db.OrderDetails.AddAsync(BuildOrderDetail("ORD-012", "P-001", 80));
        await db.MembershipSubscriptions.AddAsync(subscription);
        await db.SaveChangesAsync();

        var userMgr = UserManagerHelper.Create();
        userMgr.Setup(u => u.FindByEmailAsync(email)).ReturnsAsync(appUser);
        userMgr.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>()))
               .ReturnsAsync(IdentityResult.Success);

        var handler = new CompleteSignupHandler(
            db, BuildDateTimeMock().Object, BuildS3Mock().Object,
            BuildSponsorBonusMock().Object, BuildFastStartBonusMock().Object, userMgr.Object, BuildJwtMock().Object);

        await handler.Handle(new CompleteSignupCommand("ORD-012", BuildRequest()), CancellationToken.None);

        var updatedSub = await db.MembershipSubscriptions.FirstAsync(s => s.MemberId == memberId);
        updatedSub.SubscriptionStatus.Should().Be(MembershipStatus.Active);
    }

    [Fact]
    public async Task Handle_WhenValid_CreatesMemberStatisticsRecord()
    {
        await using var db = InMemoryDbHelper.Create();
        const string memberId = "AMB-013";
        const string email    = "amb013@example.com";

        var member       = BuildMember(memberId, email);
        var order        = BuildPendingOrder("ORD-013", memberId);
        var subscription = BuildPendingSubscription("SUB-013", memberId);
        var appUser      = BuildInactiveUser(memberId, email);

        await db.MemberProfiles.AddAsync(member);
        await db.Orders.AddAsync(order);
        await db.OrderDetails.AddAsync(BuildOrderDetail("ORD-013", "P-001", 80));
        await db.MembershipSubscriptions.AddAsync(subscription);
        await db.SaveChangesAsync();

        var userMgr = UserManagerHelper.Create();
        userMgr.Setup(u => u.FindByEmailAsync(email)).ReturnsAsync(appUser);
        userMgr.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>()))
               .ReturnsAsync(IdentityResult.Success);

        var handler = new CompleteSignupHandler(
            db, BuildDateTimeMock().Object, BuildS3Mock().Object,
            BuildSponsorBonusMock().Object, BuildFastStartBonusMock().Object, userMgr.Object, BuildJwtMock().Object);

        await handler.Handle(new CompleteSignupCommand("ORD-013", BuildRequest()), CancellationToken.None);

        var stats = await db.MemberStatistics.FirstOrDefaultAsync(s => s.MemberId == memberId);
        stats.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenValid_ReturnsAuthTokens()
    {
        await using var db = InMemoryDbHelper.Create();
        const string memberId = "AMB-014";
        const string email    = "amb014@example.com";

        var member       = BuildMember(memberId, email);
        var order        = BuildPendingOrder("ORD-014", memberId);
        var subscription = BuildPendingSubscription("SUB-014", memberId);
        var appUser      = BuildInactiveUser(memberId, email);

        await db.MemberProfiles.AddAsync(member);
        await db.Orders.AddAsync(order);
        await db.OrderDetails.AddAsync(BuildOrderDetail("ORD-014", "P-001", 80));
        await db.MembershipSubscriptions.AddAsync(subscription);
        await db.SaveChangesAsync();

        var userMgr = UserManagerHelper.Create();
        userMgr.Setup(u => u.FindByEmailAsync(email)).ReturnsAsync(appUser);
        userMgr.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>()))
               .ReturnsAsync(IdentityResult.Success);

        var handler = new CompleteSignupHandler(
            db, BuildDateTimeMock().Object, BuildS3Mock().Object,
            BuildSponsorBonusMock().Object, BuildFastStartBonusMock().Object, userMgr.Object, BuildJwtMock().Object);

        var result = await handler.Handle(
            new CompleteSignupCommand("ORD-014", BuildRequest()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().Be("mock-access-token");
        result.Value.RefreshToken.Should().Be("mock-refresh-token");
        result.Value.TokenExpiry.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenCreditCardPayment_StoresCreditCard()
    {
        await using var db = InMemoryDbHelper.Create();
        const string memberId = "AMB-015";
        const string email    = "amb015@example.com";

        var member       = BuildMember(memberId, email);
        var order        = BuildPendingOrder("ORD-015", memberId);
        var subscription = BuildPendingSubscription("SUB-015", memberId);
        var appUser      = BuildInactiveUser(memberId, email);

        await db.MemberProfiles.AddAsync(member);
        await db.Orders.AddAsync(order);
        await db.OrderDetails.AddAsync(BuildOrderDetail("ORD-015", "P-001", 80));
        await db.MembershipSubscriptions.AddAsync(subscription);
        await db.SaveChangesAsync();

        var userMgr = UserManagerHelper.Create();
        userMgr.Setup(u => u.FindByEmailAsync(email)).ReturnsAsync(appUser);
        userMgr.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>()))
               .ReturnsAsync(IdentityResult.Success);

        var handler = new CompleteSignupHandler(
            db, BuildDateTimeMock().Object, BuildS3Mock().Object,
            BuildSponsorBonusMock().Object, BuildFastStartBonusMock().Object, userMgr.Object, BuildJwtMock().Object);

        var ccRequest = new CompleteSignupRequest
        {
            PaymentMethod = PaymentMethodType.CreditCard,
            CreditCard    = new CreditCardInfoDto
            {
                Last4        = "4242",
                First6       = "411111",
                CardBrand    = "Visa",
                ExpiryMonth  = 12,
                ExpiryYear   = 2028,
                Gateway      = "Stripe",
                GatewayToken = "tok_visa",
                CardToken    = "pm_test123"
            }
        };

        await handler.Handle(new CompleteSignupCommand("ORD-015", ccRequest), CancellationToken.None);

        var storedCard = await db.CreditCards.FirstOrDefaultAsync(c => c.MemberId == memberId);
        storedCard.Should().NotBeNull();
        storedCard!.Last4.Should().Be("4242");
        storedCard.MaskedCardNumber.Should().Be("411111******4242");
        storedCard.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenValid_ReturnsSignupIdMatchingOrderId()
    {
        await using var db = InMemoryDbHelper.Create();
        const string memberId = "AMB-016";
        const string email    = "amb016@example.com";

        var member       = BuildMember(memberId, email);
        var order        = BuildPendingOrder("ORD-016", memberId);
        var subscription = BuildPendingSubscription("SUB-016", memberId);
        var appUser      = BuildInactiveUser(memberId, email);

        await db.MemberProfiles.AddAsync(member);
        await db.Orders.AddAsync(order);
        await db.OrderDetails.AddAsync(BuildOrderDetail("ORD-016", "P-001", 80));
        await db.MembershipSubscriptions.AddAsync(subscription);
        await db.SaveChangesAsync();

        var userMgr = UserManagerHelper.Create();
        userMgr.Setup(u => u.FindByEmailAsync(email)).ReturnsAsync(appUser);
        userMgr.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>()))
               .ReturnsAsync(IdentityResult.Success);

        var handler = new CompleteSignupHandler(
            db, BuildDateTimeMock().Object, BuildS3Mock().Object,
            BuildSponsorBonusMock().Object, BuildFastStartBonusMock().Object, userMgr.Object, BuildJwtMock().Object);

        var result = await handler.Handle(
            new CompleteSignupCommand("ORD-016", BuildRequest()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SignupId.Should().Be("ORD-016");
        result.Value.MemberId.Should().Be(memberId);
        result.Value.Email.Should().Be(email);
    }
}

