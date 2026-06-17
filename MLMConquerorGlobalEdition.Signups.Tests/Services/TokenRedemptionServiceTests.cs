using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Services;

/// <summary>
/// Tests for TokenRedemptionService — the unit that turns a valid token redemption into:
///  • TokenTransaction.Status = Used
///  • TokenTransaction.UsedByMemberId / UsedAt / UsedOnOrderId
///  • A new ledger TokenTransaction row (TransactionType=Used, ReferenceId=null)
///  • TokenBalance.Balance decremented for the previous owner
///
/// And rejects invalid redemptions with the right error code (generic vs. specific).
/// </summary>
public class TokenRedemptionServiceTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc);
    private const string Sponsor   = "AMB-000001";
    private const string Other     = "AMB-999999";
    private const string NewMember = "AMB-NEW0001";
    private const string OrderId   = "order-001";
    private const string VipId     = "00000002-prod-0000-0000-000000000002";
    private const string EliteId   = "00000003-prod-0000-0000-000000000003";

    private static async Task<AppDbContext> SeedAsync(
        TokenInstanceStatus status = TokenInstanceStatus.Issued,
        string ownerId             = Sponsor,
        TokenCategory category     = TokenCategory.Enrollment,
        DateTime? expiresAt        = null,
        string grantedProductId    = VipId,
        int initialBalance         = 1)
    {
        var db = InMemoryDbHelper.Create();

        await db.TokenTypes.AddAsync(new TokenType
        {
            Id           = 13,
            Name         = "Enrollment: VIP",
            Category     = category,
            IsActive     = true,
            CreatedBy    = "seed",
            CreationDate = FixedNow.AddDays(-10)
        });

        await db.TokenTypeProducts.AddAsync(new TokenTypeProduct
        {
            Id              = 1,
            TokenTypeId     = 13,
            ProductId       = grantedProductId,
            Role            = TokenProductRole.Granted,
            QuantityGranted = 1,
            CreatedBy       = "seed",
            CreationDate    = FixedNow.AddDays(-10)
        });

        await db.Products.AddAsync(new Product
        {
            Id           = VipId,
            Name         = "Travel Advantage VIP",
            Description  = "VIP",
            ImageUrl     = "",
            MonthlyFee   = 40m,
            SetupFee     = 0m,
            CreatedBy    = "seed",
            CreationDate = FixedNow,
            LastUpdateDate = FixedNow
        });

        await db.TokenTransactions.AddAsync(new TokenTransaction
        {
            Id                    = 1,
            MemberId              = ownerId,
            TokenTypeId           = 13,
            TransactionType       = TokenTransactionType.AdminGranted,
            Quantity              = 1,
            ReferenceId           = "X4P2A9N",
            Status                = status,
            OriginalOwnerMemberId = ownerId,
            ExpiresAt             = expiresAt,
            CreatedBy             = "seed",
            CreationDate          = FixedNow.AddDays(-1)
        });

        if (initialBalance > 0)
        {
            await db.TokenBalances.AddAsync(new TokenBalance
            {
                Id           = Guid.NewGuid().ToString(),
                MemberId     = ownerId,
                TokenTypeId  = 13,
                Balance      = initialBalance,
                CreatedBy    = "seed",
                CreationDate = FixedNow.AddDays(-1)
            });
        }

        await db.SaveChangesAsync();
        return db;
    }

    private static TokenRedemptionService MakeService(AppDbContext db)
        => new(db, NullLogger<TokenRedemptionService>.Instance);

    [Fact]
    public async Task RedeemForSignup_WhenAllValid_MarksUsedAndWritesLedgerAndDecrementsBalance()
    {
        await using var db = await SeedAsync();
        var svc = MakeService(db);

        var result = await svc.RedeemForSignupAsync(
            "X4P2A9N", NewMember, OrderId,
            new[] { VipId }, FixedNow, CancellationToken.None);

        await db.SaveChangesAsync();

        result.IsSuccess.Should().BeTrue();

        var instance = await db.TokenTransactions.AsNoTracking()
            .FirstAsync(t => t.ReferenceId == "X4P2A9N");
        instance.Status.Should().Be(TokenInstanceStatus.Used);
        instance.UsedByMemberId.Should().Be(NewMember);
        instance.UsedAt.Should().Be(FixedNow);
        instance.UsedOnOrderId.Should().Be(OrderId);

        var ledger = await db.TokenTransactions.AsNoTracking()
            .Where(t => t.ReferenceId == null && t.TransactionType == TokenTransactionType.Used)
            .ToListAsync();
        ledger.Should().ContainSingle();
        ledger[0].MemberId.Should().Be(Sponsor);
        ledger[0].UsedByMemberId.Should().Be(NewMember);
        ledger[0].UsedOnOrderId.Should().Be(OrderId);

        var balance = await db.TokenBalances.AsNoTracking()
            .FirstAsync(tb => tb.MemberId == Sponsor && tb.TokenTypeId == 13);
        balance.Balance.Should().Be(0);
    }

    [Fact]
    public async Task RedeemForSignup_WhenCodeNotFound_ReturnsTokenNotValid()
    {
        await using var db = await SeedAsync();
        var svc = MakeService(db);

        var result = await svc.RedeemForSignupAsync(
            "BADCODE", NewMember, OrderId,
            new[] { VipId }, FixedNow, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_NOT_VALID");
        result.Error.Should().Be("This token is not valid for this signup.");
    }

    [Fact]
    public async Task RedeemForSignup_WhenStatusUsed_ReturnsTokenNotValid_GenericMessage()
    {
        await using var db = await SeedAsync(status: TokenInstanceStatus.Used);
        var svc = MakeService(db);

        var result = await svc.RedeemForSignupAsync(
            "X4P2A9N", NewMember, OrderId,
            new[] { VipId }, FixedNow, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_NOT_VALID");
    }

    [Fact]
    public async Task RedeemForSignup_WhenTokenOwnerIsNotSponsor_StillSucceedsAndCreditsOwner()
    {
        // A token shared down the owner's downline is redeemed for a signup that is
        // NOT placed under the owner. Ownership is no longer tied to the sponsor, so
        // the redemption succeeds and the ledger/balance are credited to the true owner.
        await using var db = await SeedAsync(ownerId: Other);
        var svc = MakeService(db);

        var result = await svc.RedeemForSignupAsync(
            "X4P2A9N", NewMember, OrderId,
            new[] { VipId }, FixedNow, CancellationToken.None);

        await db.SaveChangesAsync();

        result.IsSuccess.Should().BeTrue();

        var instance = await db.TokenTransactions.AsNoTracking()
            .FirstAsync(t => t.ReferenceId == "X4P2A9N");
        instance.Status.Should().Be(TokenInstanceStatus.Used);
        instance.UsedByMemberId.Should().Be(NewMember);

        var ledger = await db.TokenTransactions.AsNoTracking()
            .Where(t => t.ReferenceId == null && t.TransactionType == TokenTransactionType.Used)
            .ToListAsync();
        ledger.Should().ContainSingle();
        ledger[0].MemberId.Should().Be(Other);

        var balance = await db.TokenBalances.AsNoTracking()
            .FirstAsync(tb => tb.MemberId == Other && tb.TokenTypeId == 13);
        balance.Balance.Should().Be(0);
    }

    [Fact]
    public async Task RedeemForSignup_WhenSelectedProductsNotInGrantedSet_ReturnsSpecificMismatch()
    {
        await using var db = await SeedAsync();
        var svc = MakeService(db);

        var result = await svc.RedeemForSignupAsync(
            "X4P2A9N", NewMember, OrderId,
            new[] { EliteId }, FixedNow, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_PRODUCT_MISMATCH");
        // Specific message names the allowed product.
        result.Error.Should().Contain("Travel Advantage VIP");
    }

    [Fact]
    public async Task RedeemForSignup_WhenExpired_ReturnsTokenNotValid()
    {
        await using var db = await SeedAsync(expiresAt: FixedNow.AddDays(-1));
        var svc = MakeService(db);

        var result = await svc.RedeemForSignupAsync(
            "X4P2A9N", NewMember, OrderId,
            new[] { VipId }, FixedNow, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_NOT_VALID");
    }

    [Fact]
    public async Task RedeemForSignup_WhenAlreadyConsumed_DoubleSpendIsBlocked()
    {
        // First call consumes the token.
        await using var db = await SeedAsync();
        var svc = MakeService(db);

        var first = await svc.RedeemForSignupAsync(
            "X4P2A9N", NewMember, OrderId,
            new[] { VipId }, FixedNow, CancellationToken.None);

        await db.SaveChangesAsync();
        first.IsSuccess.Should().BeTrue();

        // Second call should now see Status=Used and reject.
        var second = await svc.RedeemForSignupAsync(
            "X4P2A9N", "AMB-NEW0002", "order-002",
            new[] { VipId }, FixedNow, CancellationToken.None);

        second.IsSuccess.Should().BeFalse();
        second.ErrorCode.Should().Be("TOKEN_NOT_VALID");
    }

    [Fact]
    public async Task RedeemForSignup_WhenTokenCodeIsBlank_ReturnsTokenNotValid()
    {
        await using var db = await SeedAsync();
        var svc = MakeService(db);

        var result = await svc.RedeemForSignupAsync(
            "  ", NewMember, OrderId,
            new[] { VipId }, FixedNow, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_NOT_VALID");
    }
}
