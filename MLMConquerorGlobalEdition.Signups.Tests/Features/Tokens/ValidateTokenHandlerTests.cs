using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Tokens.ValidateToken;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Tokens;

/// <summary>
/// Tests for the real-time token validation endpoint. The handler ALWAYS returns Result.Success
/// at the application layer (so the HTTP response is 200) — the failure information lives inside
/// ValidateTokenResponse.Valid + .Message. Generic-message failures must collapse to the same
/// "TOKEN_NOT_VALID" body to prevent enumeration; product mismatch is the one specific case.
/// </summary>
public class ValidateTokenHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc);
    private const string SponsorId   = "AMB-000001";
    private const string OtherMember = "AMB-999999";
    private const string Slug        = "johndoe";
    private const string VipId       = "00000002-prod-0000-0000-000000000002";
    private const string EliteId     = "00000003-prod-0000-0000-000000000003";

    private static Mock<IDateTimeProvider> DateTimeProvider()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static async Task<AppDbContext> SeedAsync(
        TokenInstanceStatus status      = TokenInstanceStatus.Issued,
        string?             ownerId     = null,
        TokenCategory       category    = TokenCategory.Enrollment,
        DateTime?           expiresAt   = null,
        string?             grantedProductId = VipId)
    {
        var db = InMemoryDbHelper.Create();

        await db.MemberProfiles.AddAsync(new MemberProfile
        {
            MemberId          = SponsorId,
            FirstName         = "John",
            LastName          = "Doe",
            ReplicateSiteSlug = Slug,
            MemberType        = MemberType.Ambassador,
            EnrollDate        = FixedNow.AddYears(-1),
            Country           = "US",
            CreatedBy         = "seed",
            LastUpdateDate    = FixedNow
        });

        await db.TokenTypes.AddAsync(new TokenType
        {
            Id          = 13,
            Name        = "Enrollment: VIP",
            Category    = category,
            IsActive    = true,
            CreatedBy   = "seed",
            CreationDate= FixedNow.AddDays(-10)
        });

        await db.TokenTypeProducts.AddAsync(new TokenTypeProduct
        {
            Id              = 1,
            TokenTypeId     = 13,
            ProductId       = grantedProductId ?? VipId,
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
            MemberId              = ownerId ?? SponsorId,
            TokenTypeId           = 13,
            TransactionType       = TokenTransactionType.AdminGranted,
            Quantity              = 1,
            ReferenceId           = "X4P2A9N",
            Status                = status,
            OriginalOwnerMemberId = ownerId ?? SponsorId,
            ExpiresAt             = expiresAt,
            CreatedBy             = "seed",
            CreationDate          = FixedNow.AddDays(-1)
        });

        await db.SaveChangesAsync();
        return db;
    }

    private static ValidateTokenHandler MakeHandler(AppDbContext db)
        => new(db, DateTimeProvider().Object, NullLogger<ValidateTokenHandler>.Instance);

    [Fact]
    public async Task Handle_WhenAllValid_ReturnsValidWithAllowedProductIds()
    {
        await using var db = await SeedAsync();
        var handler = MakeHandler(db);

        var result = await handler.Handle(new ValidateTokenQuery(new ValidateTokenRequest
        {
            Code                 = "X4P2A9N",
            SponsorReplicateSite = Slug,
            SelectedProductIds   = new List<string> { VipId }
        }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Valid.Should().BeTrue();
        result.Value.AllowedProductIds.Should().ContainSingle(p => p == VipId);
        result.Value.AllowedProductNames.Should().ContainSingle(n => n == "Travel Advantage VIP");
        result.Value.TokenTypeId.Should().Be(13);
    }

    [Fact]
    public async Task Handle_WhenSponsorSlugUnknown_ReturnsGenericMessage()
    {
        await using var db = await SeedAsync();
        var handler = MakeHandler(db);

        var result = await handler.Handle(new ValidateTokenQuery(new ValidateTokenRequest
        {
            Code                 = "X4P2A9N",
            SponsorReplicateSite = "ghost-sponsor",
            SelectedProductIds   = new List<string> { VipId }
        }), CancellationToken.None);

        result.Value!.Valid.Should().BeFalse();
        result.Value.Message.Should().Be("This token is not valid for this signup.");
        result.Value.AllowedProductIds.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenCodeNotFound_ReturnsGenericMessage()
    {
        await using var db = await SeedAsync();
        var handler = MakeHandler(db);

        var result = await handler.Handle(new ValidateTokenQuery(new ValidateTokenRequest
        {
            Code                 = "NOPE000",
            SponsorReplicateSite = Slug,
            SelectedProductIds   = new List<string> { VipId }
        }), CancellationToken.None);

        result.Value!.Valid.Should().BeFalse();
        result.Value.Message.Should().Be("This token is not valid for this signup.");
    }

    [Fact]
    public async Task Handle_WhenStatusUsed_ReturnsGenericMessage()
    {
        await using var db = await SeedAsync(status: TokenInstanceStatus.Used);
        var handler = MakeHandler(db);

        var result = await handler.Handle(new ValidateTokenQuery(new ValidateTokenRequest
        {
            Code                 = "X4P2A9N",
            SponsorReplicateSite = Slug,
            SelectedProductIds   = new List<string> { VipId }
        }), CancellationToken.None);

        result.Value!.Valid.Should().BeFalse();
        // Generic — must not reveal that the token was already used.
        result.Value.Message.Should().Be("This token is not valid for this signup.");
    }

    [Fact]
    public async Task Handle_WhenOwnerIsNotSponsor_ReturnsGenericMessage()
    {
        await using var db = await SeedAsync(ownerId: OtherMember);
        var handler = MakeHandler(db);

        var result = await handler.Handle(new ValidateTokenQuery(new ValidateTokenRequest
        {
            Code                 = "X4P2A9N",
            SponsorReplicateSite = Slug,
            SelectedProductIds   = new List<string> { VipId }
        }), CancellationToken.None);

        result.Value!.Valid.Should().BeFalse();
        result.Value.Message.Should().Be("This token is not valid for this signup.");
    }

    [Fact]
    public async Task Handle_WhenExpired_ReturnsGenericMessage()
    {
        await using var db = await SeedAsync(expiresAt: FixedNow.AddDays(-1));
        var handler = MakeHandler(db);

        var result = await handler.Handle(new ValidateTokenQuery(new ValidateTokenRequest
        {
            Code                 = "X4P2A9N",
            SponsorReplicateSite = Slug,
            SelectedProductIds   = new List<string> { VipId }
        }), CancellationToken.None);

        result.Value!.Valid.Should().BeFalse();
        result.Value.Message.Should().Be("This token is not valid for this signup.");
    }

    [Fact]
    public async Task Handle_WhenSelectedProductsNotInGrantedSet_ReturnsSpecificProductMismatchMessage()
    {
        // Token grants only VIP. User selects Elite → product mismatch.
        await using var db = await SeedAsync();
        var handler = MakeHandler(db);

        var result = await handler.Handle(new ValidateTokenQuery(new ValidateTokenRequest
        {
            Code                 = "X4P2A9N",
            SponsorReplicateSite = Slug,
            SelectedProductIds   = new List<string> { EliteId }
        }), CancellationToken.None);

        result.Value!.Valid.Should().BeFalse();
        // Specific message — names the allowed product.
        result.Value.Message.Should().Contain("Travel Advantage VIP");
    }

    [Fact]
    public async Task Handle_WhenCodeMatchedCaseInsensitive_Succeeds()
    {
        await using var db = await SeedAsync();
        var handler = MakeHandler(db);

        var result = await handler.Handle(new ValidateTokenQuery(new ValidateTokenRequest
        {
            Code                 = "x4p2a9n",  // lowercased
            SponsorReplicateSite = Slug,
            SelectedProductIds   = new List<string> { VipId }
        }), CancellationToken.None);

        result.Value!.Valid.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenSponsorMemberIdInsteadOfSlug_Succeeds()
    {
        // Frontend may pass either replicate-site slug or raw MemberId.
        await using var db = await SeedAsync();
        var handler = MakeHandler(db);

        var result = await handler.Handle(new ValidateTokenQuery(new ValidateTokenRequest
        {
            Code                 = "X4P2A9N",
            SponsorReplicateSite = SponsorId,
            SelectedProductIds   = new List<string> { VipId }
        }), CancellationToken.None);

        result.Value!.Valid.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenTokenIsUpgradeCategory_ReturnsGenericMessage()
    {
        // Upgrade tokens are not valid in a NEW signup flow.
        await using var db = await SeedAsync(category: TokenCategory.Upgrade);
        var handler = MakeHandler(db);

        var result = await handler.Handle(new ValidateTokenQuery(new ValidateTokenRequest
        {
            Code                 = "X4P2A9N",
            SponsorReplicateSite = Slug,
            SelectedProductIds   = new List<string> { VipId }
        }), CancellationToken.None);

        result.Value!.Valid.Should().BeFalse();
        result.Value.Message.Should().Be("This token is not valid for this signup.");
    }
}
