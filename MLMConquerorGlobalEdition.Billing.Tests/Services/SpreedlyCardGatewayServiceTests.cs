using FluentAssertions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services.CardGateway;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services;

/// <summary>
/// Unit tests for SpreedlyCardGatewayService — the universal Spreedly proxy.
///
/// Per BILLING-RULES §3:
///  - Requires the member's SpreedlyPaymentMethodToken (from MemberCreditCard).
///  - Requires the "Spreedly" ApiCredential (API auth key).
///  - Requires the per-processor ApiCredential.SpreedlyGatewayTokenEncrypted.
///  - On success: returns a simulated transaction ID starting with "simulated-spreedly-txn-".
/// </summary>
public class SpreedlyCardGatewayServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ILogger<SpreedlyCardGatewayService> Logger()
        => new Mock<ILogger<SpreedlyCardGatewayService>>().Object;

    private static GatewayChargeRequest MakeRequest(string? spreedlyToken = "spm_test_token_123") => new()
    {
        MemberId                  = "member-1",
        Amount                    = 99m,
        Currency                  = "USD",
        Description               = "Test recurring charge",
        TokenizedCardRef          = "tok_abc",
        NetworkTransactionId      = "ntxn_abc",
        IsRecurring               = true,
        SpreedlyPaymentMethodToken = spreedlyToken,
        DownstreamProcessor       = CardProcessor.NmiSpreedly
    };

    private static async Task SeedSpreedlyCredentialAsync(
        Repository.Context.AppDbContext db,
        bool active = true,
        string? apiKey = "ENC:test-spreedly-env-key")
    {
        var cred = new ApiCredential
        {
            ServiceKey   = "Spreedly",
            IsActive     = active,
            Environment  = "Production",
            BaseUrl      = "https://core.spreedly.com",
            CreationDate = DateTime.UtcNow,
            CreatedBy    = "test",
            LastUpdateDate = DateTime.UtcNow
        };
        if (apiKey is not null)
            cred.ApiKeyEncrypted = apiKey;
        db.ApiCredentials.Add(cred);
        await db.SaveChangesAsync();
    }

    private static async Task SeedProcessorCredentialAsync(
        Repository.Context.AppDbContext db,
        CardProcessor processor = CardProcessor.NmiSpreedly,
        string? gatewayToken = "ENC:spreedly-gw-token-nmi")
    {
        var cred = new ApiCredential
        {
            ServiceKey   = processor.ToString(),
            IsActive     = true,
            Environment  = "Production",
            CreationDate = DateTime.UtcNow,
            CreatedBy    = "test",
            LastUpdateDate = DateTime.UtcNow
        };
        if (gatewayToken is not null)
            cred.SpreedlyGatewayTokenEncrypted = gatewayToken;
        db.ApiCredentials.Add(cred);
        await db.SaveChangesAsync();
    }

    // ── Missing SpreedlyPaymentMethodToken → failure ──────────────────────

    [Fact]
    public async Task ChargeAsync_WhenSpreedlyTokenMissing_ReturnsMemberTokenMissingFailure()
    {
        using var db  = TestDbContextFactory.Create();
        var svc = new SpreedlyCardGatewayService(db, Logger());

        var req = MakeRequest(spreedlyToken: null);
        var result = await svc.ChargeWithProcessorAsync(req, CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SPREEDLY_MEMBER_TOKEN_MISSING");
    }

    [Fact]
    public async Task ChargeAsync_WhenSpreedlyTokenEmpty_ReturnsMemberTokenMissingFailure()
    {
        using var db  = TestDbContextFactory.Create();
        var svc = new SpreedlyCardGatewayService(db, Logger());

        var req = MakeRequest(spreedlyToken: string.Empty);
        var result = await svc.ChargeWithProcessorAsync(req, CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SPREEDLY_MEMBER_TOKEN_MISSING");
    }

    // ── Missing Spreedly master credential → failure ──────────────────────

    [Fact]
    public async Task ChargeAsync_WhenSpreedlyCredentialMissing_ReturnsCredentialMissingFailure()
    {
        using var db  = TestDbContextFactory.Create();
        var svc = new SpreedlyCardGatewayService(db, Logger());
        // No credentials seeded at all
        var result = await svc.ChargeWithProcessorAsync(MakeRequest(), CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SPREEDLY_CREDENTIAL_MISSING");
    }

    [Fact]
    public async Task ChargeAsync_WhenSpreedlyCredentialInactive_ReturnsCredentialMissingFailure()
    {
        using var db  = TestDbContextFactory.Create();
        await SeedSpreedlyCredentialAsync(db, active: false);
        var svc = new SpreedlyCardGatewayService(db, Logger());

        var result = await svc.ChargeWithProcessorAsync(MakeRequest(), CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SPREEDLY_CREDENTIAL_MISSING");
    }

    [Fact]
    public async Task ChargeAsync_WhenSpreedlyApiKeyNotSet_ReturnsCredentialIncompleteFailure()
    {
        using var db  = TestDbContextFactory.Create();
        await SeedSpreedlyCredentialAsync(db, active: true, apiKey: null);
        var svc = new SpreedlyCardGatewayService(db, Logger());

        var result = await svc.ChargeWithProcessorAsync(MakeRequest(), CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SPREEDLY_CREDENTIAL_INCOMPLETE");
    }

    // ── Missing downstream gateway token → failure ────────────────────────

    [Fact]
    public async Task ChargeAsync_WhenDownstreamGatewayTokenMissing_ReturnsDownstreamTokenMissingFailure()
    {
        using var db  = TestDbContextFactory.Create();
        await SeedSpreedlyCredentialAsync(db);
        // No processor credential seeded
        var svc = new SpreedlyCardGatewayService(db, Logger());

        var result = await svc.ChargeWithProcessorAsync(MakeRequest(), CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SPREEDLY_DOWNSTREAM_TOKEN_MISSING");
    }

    [Fact]
    public async Task ChargeAsync_WhenDownstreamGatewayTokenNotSet_ReturnsDownstreamTokenNotSetFailure()
    {
        using var db  = TestDbContextFactory.Create();
        await SeedSpreedlyCredentialAsync(db);
        await SeedProcessorCredentialAsync(db, gatewayToken: null);   // row exists but no token
        var svc = new SpreedlyCardGatewayService(db, Logger());

        var result = await svc.ChargeWithProcessorAsync(MakeRequest(), CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SPREEDLY_DOWNSTREAM_TOKEN_NOT_SET");
    }

    // ── Happy path: both tokens present → simulated success ──────────────

    [Fact]
    public async Task ChargeAsync_WhenAllTokensPresent_ReturnsSimulatedSuccess()
    {
        using var db  = TestDbContextFactory.Create();
        await SeedSpreedlyCredentialAsync(db);
        await SeedProcessorCredentialAsync(db);
        var svc = new SpreedlyCardGatewayService(db, Logger());

        var result = await svc.ChargeWithProcessorAsync(MakeRequest(), CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeTrue();
        result.Value!.GatewayTransactionId.Should().StartWith("simulated-spreedly-txn-");
        result.Value.Status.Should().Be("simulated_success");
    }

    [Fact]
    public async Task ChargeAsync_WhenAllTokensPresent_RawResponseContainsProcessor()
    {
        using var db  = TestDbContextFactory.Create();
        await SeedSpreedlyCredentialAsync(db);
        await SeedProcessorCredentialAsync(db, CardProcessor.CheckoutEUR, "ENC:spreedly-gw-token-checkout-eur");
        var svc = new SpreedlyCardGatewayService(db, Logger());

        var req = new GatewayChargeRequest
        {
            MemberId                  = "member-1",
            Amount                    = 99m,
            Currency                  = "USD",
            IsRecurring               = true,
            SpreedlyPaymentMethodToken = "spm_test_token_123",
            DownstreamProcessor       = CardProcessor.CheckoutEUR
        };
        var result = await svc.ChargeWithProcessorAsync(req, CardProcessor.CheckoutEUR);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RawResponse.Should().Contain("CheckoutEUR");
    }

    // ── Refund: both tokens present → success ─────────────────────────────

    [Fact]
    public async Task RefundAsync_WhenAllTokensPresent_ReturnsSuccess()
    {
        using var db  = TestDbContextFactory.Create();
        await SeedSpreedlyCredentialAsync(db);
        await SeedProcessorCredentialAsync(db);
        var svc = new SpreedlyCardGatewayService(db, Logger());

        var result = await svc.RefundWithProcessorAsync("txn-123", 50m, CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task RefundAsync_WhenSpreedlyCredentialMissing_ReturnsFailure()
    {
        using var db  = TestDbContextFactory.Create();
        var svc = new SpreedlyCardGatewayService(db, Logger());

        var result = await svc.RefundWithProcessorAsync("txn-123", 50m, CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SPREEDLY_CREDENTIAL_MISSING");
    }

    // ── Request shape validation ──────────────────────────────────────────

    [Fact]
    public async Task ChargeAsync_RequestShape_ContainsMemberTokenAndDownstreamProcessor()
    {
        using var db  = TestDbContextFactory.Create();
        await SeedSpreedlyCredentialAsync(db);
        await SeedProcessorCredentialAsync(db, CardProcessor.Shift4, "ENC:spreedly-gw-token-shift4");
        var svc = new SpreedlyCardGatewayService(db, Logger());

        var req = new GatewayChargeRequest
        {
            MemberId                  = "member-42",
            Amount                    = 149.99m,
            Currency                  = "USD",
            IsRecurring               = true,
            SpreedlyPaymentMethodToken = "spm_shift4_member42",
            DownstreamProcessor       = CardProcessor.Shift4,
            NetworkTransactionId      = "ntxn_prior_456"
        };

        var result = await svc.ChargeWithProcessorAsync(req, CardProcessor.Shift4);

        result.IsSuccess.Should().BeTrue();
        result.Value!.GatewayTransactionId.Should().StartWith("simulated-spreedly-txn-");
    }
}
