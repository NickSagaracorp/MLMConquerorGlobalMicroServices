using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.Signups.DTOs;
using MLMConquerorGlobalEdition.Signups.Features.Signups.Commands.SelectProducts;
using MLMConquerorGlobalEdition.Signups.Tests.Helpers;

namespace MLMConquerorGlobalEdition.Signups.Tests.Features.Signups;

public class SelectProductsHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 25, 10, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> DateTime() =>
        new Mock<IDateTimeProvider>().Also(m => m.Setup(d => d.Now).Returns(FixedNow));

    private static MemberProfile BuildMember(string memberId, string email = "test@example.com") => new()
    {
        MemberId       = memberId,
        Email          = email,
        FirstName      = "Test",
        LastName       = "User",
        MemberType     = MemberType.Ambassador,
        Status         = MemberAccountStatus.Pending,
        EnrollDate     = FixedNow,
        Country        = "US",
        CreatedBy      = email,
        CreationDate   = FixedNow,
        LastUpdateDate = FixedNow
    };

    private static Orders BuildPendingOrder(string orderId, string memberId) => new()
    {
        Id             = orderId,
        MemberId       = memberId,
        TotalAmount    = 0,
        Status         = OrderStatus.Pending,
        OrderDate      = FixedNow,
        CreatedBy      = "test@example.com",
        CreationDate   = FixedNow,
        LastUpdateDate = FixedNow
    };

    private static Product BuildProduct(string id, decimal setupFee = 50, decimal monthlyFee = 30) => new()
    {
        Id           = id,
        Name         = $"Product {id}",
        Description  = "Test product",
        ImageUrl     = "https://example.com/img.png",
        SetupFee     = setupFee,
        MonthlyFee   = monthlyFee,
        IsActive     = true,
        CreatedBy    = "seed",
        CreationDate = FixedNow,
        LastUpdateDate = FixedNow
    };

    // ── Failure cases ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSignupNotFound_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new SelectProductsHandler(db, DateTime().Object);
        var request = new SelectProductsRequest { ProductIds = ["P-001"] };

        var result = await handler.Handle(
            new SelectProductsCommand("NON-EXISTENT-ORDER", request), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SIGNUP_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenOrderIsNotPending_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var member = BuildMember("AMB-000001");
        var order = BuildPendingOrder("ORDER-001", "AMB-000001");
        order.Status = OrderStatus.Completed;

        await db.MemberProfiles.AddAsync(member);
        await db.Orders.AddAsync(order);
        await db.SaveChangesAsync();

        var handler = new SelectProductsHandler(db, DateTime().Object);
        var request = new SelectProductsRequest { ProductIds = ["P-001"] };

        var result = await handler.Handle(
            new SelectProductsCommand("ORDER-001", request), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SIGNUP_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenNoValidProductsProvided_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var member = BuildMember("AMB-000002");
        var order = BuildPendingOrder("ORDER-002", "AMB-000002");

        await db.MemberProfiles.AddAsync(member);
        await db.Orders.AddAsync(order);
        await db.SaveChangesAsync();

        var handler = new SelectProductsHandler(db, DateTime().Object);
        var request = new SelectProductsRequest { ProductIds = ["INVALID-PRODUCT"] };

        var result = await handler.Handle(
            new SelectProductsCommand("ORDER-002", request), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_VALID_PRODUCTS");
    }

    [Fact]
    public async Task Handle_WhenProductIsInactive_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var member = BuildMember("AMB-000003");
        var order = BuildPendingOrder("ORDER-003", "AMB-000003");
        var inactiveProduct = BuildProduct("P-INACTIVE");
        inactiveProduct.IsActive = false;

        await db.MemberProfiles.AddAsync(member);
        await db.Orders.AddAsync(order);
        await db.Products.AddAsync(inactiveProduct);
        await db.SaveChangesAsync();

        var handler = new SelectProductsHandler(db, DateTime().Object);
        var request = new SelectProductsRequest { ProductIds = ["P-INACTIVE"] };

        var result = await handler.Handle(
            new SelectProductsCommand("ORDER-003", request), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_VALID_PRODUCTS");
    }

    // ── Success cases ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenValidProducts_ReturnsSuccess()
    {
        await using var db = InMemoryDbHelper.Create();
        var member = BuildMember("AMB-000004");
        var order = BuildPendingOrder("ORDER-004", "AMB-000004");
        var product = BuildProduct("P-001", 50, 30);

        await db.MemberProfiles.AddAsync(member);
        await db.Orders.AddAsync(order);
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        var handler = new SelectProductsHandler(db, DateTime().Object);
        var request = new SelectProductsRequest { ProductIds = ["P-001"] };

        var result = await handler.Handle(
            new SelectProductsCommand("ORDER-004", request), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenValidProducts_CreatesOrderDetails()
    {
        await using var db = InMemoryDbHelper.Create();
        var member = BuildMember("AMB-000005");
        var order = BuildPendingOrder("ORDER-005", "AMB-000005");
        var product = BuildProduct("P-002", 50, 30);

        await db.MemberProfiles.AddAsync(member);
        await db.Orders.AddAsync(order);
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        var handler = new SelectProductsHandler(db, DateTime().Object);
        var request = new SelectProductsRequest { ProductIds = ["P-002"] };

        await handler.Handle(new SelectProductsCommand("ORDER-005", request), CancellationToken.None);

        var details = await db.OrderDetails.Where(d => d.OrderId == "ORDER-005").ToListAsync();
        details.Should().HaveCount(1);
        details[0].ProductId.Should().Be("P-002");
        details[0].UnitPrice.Should().Be(80); // 50 + 30
    }

    [Fact]
    public async Task Handle_WhenValidProducts_UpdatesOrderTotal()
    {
        await using var db = InMemoryDbHelper.Create();
        var member = BuildMember("AMB-000006");
        var order = BuildPendingOrder("ORDER-006", "AMB-000006");
        var p1 = BuildProduct("P-010", 50, 30);
        var p2 = BuildProduct("P-011", 100, 50);

        await db.MemberProfiles.AddAsync(member);
        await db.Orders.AddAsync(order);
        await db.Products.AddRangeAsync(p1, p2);
        await db.SaveChangesAsync();

        var handler = new SelectProductsHandler(db, DateTime().Object);
        var request = new SelectProductsRequest { ProductIds = ["P-010", "P-011"] };

        await handler.Handle(new SelectProductsCommand("ORDER-006", request), CancellationToken.None);

        var updatedOrder = await db.Orders.FindAsync("ORDER-006");
        updatedOrder!.TotalAmount.Should().Be(230); // (50+30) + (100+50)
    }

    [Fact]
    public async Task Handle_WhenCalledTwice_ReplacesExistingProducts()
    {
        await using var db = InMemoryDbHelper.Create();
        var member = BuildMember("AMB-000007");
        var order = BuildPendingOrder("ORDER-007", "AMB-000007");
        var p1 = BuildProduct("P-020", 50, 30);
        var p2 = BuildProduct("P-021", 100, 50);

        await db.MemberProfiles.AddAsync(member);
        await db.Orders.AddAsync(order);
        await db.Products.AddRangeAsync(p1, p2);
        await db.SaveChangesAsync();

        var handler = new SelectProductsHandler(db, DateTime().Object);

        // First selection: P-020
        await handler.Handle(
            new SelectProductsCommand("ORDER-007", new SelectProductsRequest { ProductIds = ["P-020"] }),
            CancellationToken.None);

        // Second selection: P-021 (replaces P-020)
        await handler.Handle(
            new SelectProductsCommand("ORDER-007", new SelectProductsRequest { ProductIds = ["P-021"] }),
            CancellationToken.None);

        var details = await db.OrderDetails.Where(d => d.OrderId == "ORDER-007").ToListAsync();
        details.Should().HaveCount(1);
        details[0].ProductId.Should().Be("P-021");

        var updatedOrder = await db.Orders.FindAsync("ORDER-007");
        updatedOrder!.TotalAmount.Should().Be(150); // 100 + 50
    }
}

// ── Test helpers ──────────────────────────────────────────────────────────────
internal static class MockExtensions
{
    internal static T Also<T>(this T value, Action<T> configure)
    {
        configure(value);
        return value;
    }
}
