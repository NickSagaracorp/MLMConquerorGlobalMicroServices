using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services.CardGateway;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services;

/// <summary>
/// Unit tests for SpreedlyCardGatewayService — the universal Spreedly proxy.
///
///  - Requires either an existing SpreedlyPaymentMethodToken (recurring charge) or RawCard
///    details (first-time signup charge).
///  - Requires the "Spreedly" ApiCredential (environment key + access secret).
///  - Requires the per-processor ApiCredential.SpreedlyGatewayTokenEncrypted.
///  - Talks to core.spreedly.com via IHttpClientFactory — HTTP calls are faked here.
/// </summary>
public class SpreedlyCardGatewayServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ILogger<SpreedlyCardGatewayService> Logger()
        => new Mock<ILogger<SpreedlyCardGatewayService>>().Object;

    /// <summary>Empty configuration — no "Spreedly" section, so no appsettings fallback applies.</summary>
    private static IConfiguration EmptyConfig()
        => new ConfigurationBuilder().Build();

    private static IConfiguration ConfigWithSpreedly(
        string? environmentKey = null, string? accessSecret = null, string? baseUrl = null, string? defaultGatewayToken = null)
    {
        var data = new Dictionary<string, string?>();
        if (environmentKey      is not null) data["Spreedly:EnvironmentKey"]      = environmentKey;
        if (accessSecret        is not null) data["Spreedly:AccessSecret"]        = accessSecret;
        if (baseUrl             is not null) data["Spreedly:BaseUrl"]             = baseUrl;
        if (defaultGatewayToken is not null) data["Spreedly:DefaultGatewayToken"] = defaultGatewayToken;
        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    /// <summary>Test double mirroring the "ENC:" prefix convention without real Data Protection.</summary>
    private class FakeEncryptionService : IEncryptionService
    {
        public string Encrypt(string plaintext) => "ENC:" + plaintext;

        public string Decrypt(string ciphertext)
        {
            if (!ciphertext.StartsWith("ENC:", StringComparison.Ordinal))
                throw new InvalidOperationException("Value is not encrypted.");
            return ciphertext["ENC:".Length..];
        }
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            => _responder = responder;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return _responder(request);
        }
    }

    private static IHttpClientFactory MakeHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> responder, out FakeHttpMessageHandler handler)
    {
        handler = new FakeHttpMessageHandler(responder);
        var client = new HttpClient(handler);
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("Spreedly")).Returns(client);
        return factoryMock.Object;
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object body)
        => new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };

    private static GatewayChargeRequest MakeRequest(string? spreedlyToken = "spm_test_token_123") => new()
    {
        MemberId                   = "member-1",
        Amount                     = 99m,
        Currency                   = "USD",
        Description                = "Test recurring charge",
        TokenizedCardRef           = "tok_abc",
        NetworkTransactionId       = "ntxn_abc",
        IsRecurring                = true,
        SpreedlyPaymentMethodToken = spreedlyToken,
        DownstreamProcessor        = CardProcessor.NmiSpreedly
    };

    private static async Task SeedSpreedlyCredentialAsync(
        Repository.Context.AppDbContext db,
        bool active = true,
        string? apiKey = "ENC:test-spreedly-env-key",
        string? secretKey = "ENC:test-spreedly-access-secret")
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
        if (secretKey is not null)
            cred.SecretKeyEncrypted = secretKey;
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

    // ── Missing SpreedlyPaymentMethodToken and RawCard → failure ──────────

    [Fact]
    public async Task ChargeAsync_WhenNoTokenAndNoRawCard_ReturnsMemberTokenMissingFailure()
    {
        using var db = TestDbContextFactory.Create();
        var httpFactory = MakeHttpClientFactory(_ => JsonResponse(HttpStatusCode.OK, new { }), out _);
        var svc = new SpreedlyCardGatewayService(db, httpFactory, new FakeEncryptionService(), EmptyConfig(), Logger());

        var req = MakeRequest(spreedlyToken: null);
        var result = await svc.ChargeWithProcessorAsync(req, CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SPREEDLY_MEMBER_TOKEN_MISSING");
    }

    // ── Missing Spreedly master credential → failure ──────────────────────

    [Fact]
    public async Task ChargeAsync_WhenSpreedlyCredentialMissing_ReturnsCredentialMissingFailure()
    {
        using var db = TestDbContextFactory.Create();
        var httpFactory = MakeHttpClientFactory(_ => JsonResponse(HttpStatusCode.OK, new { }), out _);
        var svc = new SpreedlyCardGatewayService(db, httpFactory, new FakeEncryptionService(), EmptyConfig(), Logger());
        // No credentials seeded at all
        var result = await svc.ChargeWithProcessorAsync(MakeRequest(), CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SPREEDLY_CREDENTIAL_MISSING");
    }

    [Fact]
    public async Task ChargeAsync_WhenSpreedlyCredentialInactive_ReturnsCredentialMissingFailure()
    {
        using var db = TestDbContextFactory.Create();
        await SeedSpreedlyCredentialAsync(db, active: false);
        var httpFactory = MakeHttpClientFactory(_ => JsonResponse(HttpStatusCode.OK, new { }), out _);
        var svc = new SpreedlyCardGatewayService(db, httpFactory, new FakeEncryptionService(), EmptyConfig(), Logger());

        var result = await svc.ChargeWithProcessorAsync(MakeRequest(), CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SPREEDLY_CREDENTIAL_MISSING");
    }

    [Fact]
    public async Task ChargeAsync_WhenSpreedlySecretKeyNotSet_ReturnsCredentialIncompleteFailure()
    {
        using var db = TestDbContextFactory.Create();
        await SeedSpreedlyCredentialAsync(db, active: true, secretKey: null);
        var httpFactory = MakeHttpClientFactory(_ => JsonResponse(HttpStatusCode.OK, new { }), out _);
        var svc = new SpreedlyCardGatewayService(db, httpFactory, new FakeEncryptionService(), EmptyConfig(), Logger());

        var result = await svc.ChargeWithProcessorAsync(MakeRequest(), CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SPREEDLY_CREDENTIAL_INCOMPLETE");
    }

    // ── appsettings.json fallback for the master credential ───────────────

    [Fact]
    public async Task ChargeAsync_WhenNoDbCredentialButAppSettingsConfigured_UsesAppSettingsAndSucceeds()
    {
        using var db = TestDbContextFactory.Create();
        // No "Spreedly" ApiCredential row seeded at all — only the per-processor row.
        await SeedProcessorCredentialAsync(db);

        var config = ConfigWithSpreedly(
            environmentKey: "config-env-key",
            accessSecret:   "config-access-secret",
            baseUrl:        "https://core.spreedly.com");

        var httpFactory = MakeHttpClientFactory(req => JsonResponse(HttpStatusCode.OK, new
        {
            transaction = new { token = "txn-from-config", succeeded = true }
        }), out var handler);

        var svc = new SpreedlyCardGatewayService(db, httpFactory, new FakeEncryptionService(), config, Logger());
        var result = await svc.ChargeWithProcessorAsync(MakeRequest(), CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeTrue();
        result.Value!.GatewayTransactionId.Should().Be("txn-from-config");

        var authHeader = handler.LastRequest!.Headers.Authorization!.Parameter;
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader!));
        decoded.Should().Be("config-env-key:config-access-secret");
    }

    [Fact]
    public async Task ChargeAsync_WhenDbHasOnlyEnvironmentKey_FallsBackToAppSettingsForAccessSecret()
    {
        using var db = TestDbContextFactory.Create();
        // DB row provides only the environment key; the access secret comes from appsettings.
        await SeedSpreedlyCredentialAsync(db, apiKey: "ENC:db-env-key", secretKey: null);
        await SeedProcessorCredentialAsync(db);

        var config = ConfigWithSpreedly(accessSecret: "config-access-secret");

        var httpFactory = MakeHttpClientFactory(req => JsonResponse(HttpStatusCode.OK, new
        {
            transaction = new { token = "txn-mixed-source", succeeded = true }
        }), out var handler);

        var svc = new SpreedlyCardGatewayService(db, httpFactory, new FakeEncryptionService(), config, Logger());
        var result = await svc.ChargeWithProcessorAsync(MakeRequest(), CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeTrue();
        var authHeader = handler.LastRequest!.Headers.Authorization!.Parameter;
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader!));
        decoded.Should().Be("db-env-key:config-access-secret");
    }

    [Fact]
    public async Task ChargeAsync_WhenAppSettingsStillHasPlaceholderValues_TreatsAsNotConfigured()
    {
        using var db = TestDbContextFactory.Create();
        // No DB row; appsettings still has the un-replaced placeholder values from the template.
        var config = ConfigWithSpreedly(
            environmentKey: "REPLACE_WITH_SPREEDLY_ENVIRONMENT_KEY",
            accessSecret:   "REPLACE_WITH_SPREEDLY_ACCESS_SECRET");

        var httpFactory = MakeHttpClientFactory(_ => JsonResponse(HttpStatusCode.OK, new { }), out _);
        var svc = new SpreedlyCardGatewayService(db, httpFactory, new FakeEncryptionService(), config, Logger());

        var result = await svc.ChargeWithProcessorAsync(MakeRequest(), CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SPREEDLY_CREDENTIAL_MISSING");
    }

    // ── Missing downstream gateway token → failure ────────────────────────

    [Fact]
    public async Task ChargeAsync_WhenDownstreamGatewayTokenMissing_ReturnsDownstreamTokenMissingFailure()
    {
        using var db = TestDbContextFactory.Create();
        await SeedSpreedlyCredentialAsync(db);
        // No processor credential seeded
        var httpFactory = MakeHttpClientFactory(_ => JsonResponse(HttpStatusCode.OK, new { }), out _);
        var svc = new SpreedlyCardGatewayService(db, httpFactory, new FakeEncryptionService(), EmptyConfig(), Logger());

        var result = await svc.ChargeWithProcessorAsync(MakeRequest(), CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SPREEDLY_DOWNSTREAM_TOKEN_MISSING");
    }

    [Fact]
    public async Task ChargeAsync_WhenDownstreamGatewayTokenNotSet_ReturnsDownstreamTokenNotSetFailure()
    {
        using var db = TestDbContextFactory.Create();
        await SeedSpreedlyCredentialAsync(db);
        await SeedProcessorCredentialAsync(db, gatewayToken: null);   // row exists but no token
        var httpFactory = MakeHttpClientFactory(_ => JsonResponse(HttpStatusCode.OK, new { }), out _);
        var svc = new SpreedlyCardGatewayService(db, httpFactory, new FakeEncryptionService(), EmptyConfig(), Logger());

        var result = await svc.ChargeWithProcessorAsync(MakeRequest(), CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SPREEDLY_DOWNSTREAM_TOKEN_NOT_SET");
    }

    [Fact]
    public async Task ChargeAsync_WhenNoProcessorRowButAppSettingsHasDefaultGatewayToken_Succeeds()
    {
        using var db = TestDbContextFactory.Create();
        await SeedSpreedlyCredentialAsync(db);
        // No per-processor ApiCredential row seeded at all.

        var config = ConfigWithSpreedly(defaultGatewayToken: "config-default-gateway-token");

        var httpFactory = MakeHttpClientFactory(req => JsonResponse(HttpStatusCode.OK, new
        {
            transaction = new { token = "txn-default-gw", succeeded = true }
        }), out var handler);

        var svc = new SpreedlyCardGatewayService(db, httpFactory, new FakeEncryptionService(), config, Logger());
        var result = await svc.ChargeWithProcessorAsync(MakeRequest(), CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeTrue();
        handler.LastRequest!.RequestUri!.ToString().Should().Contain("/v1/gateways/config-default-gateway-token/purchase.json");
    }

    // ── Happy path: existing payment_method_token → real HTTP purchase ────

    [Fact]
    public async Task ChargeAsync_WithExistingToken_PostsToPurchaseEndpointAndReturnsSuccess()
    {
        using var db = TestDbContextFactory.Create();
        await SeedSpreedlyCredentialAsync(db);
        await SeedProcessorCredentialAsync(db);

        var httpFactory = MakeHttpClientFactory(req => JsonResponse(HttpStatusCode.OK, new
        {
            transaction = new
            {
                token     = "txn-real-123",
                succeeded = true,
                message   = "Succeeded!"
            }
        }), out var handler);

        var svc = new SpreedlyCardGatewayService(db, httpFactory, new FakeEncryptionService(), EmptyConfig(), Logger());
        var result = await svc.ChargeWithProcessorAsync(MakeRequest(), CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeTrue();
        result.Value!.GatewayTransactionId.Should().Be("txn-real-123");
        result.Value.Status.Should().Be("succeeded");
        result.Value.SpreedlyPaymentMethodToken.Should().BeNull();

        handler.LastRequest!.RequestUri!.ToString().Should().Contain("/v1/gateways/spreedly-gw-token-nmi/purchase.json");
        handler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Basic");
        handler.LastRequestBody.Should().Contain("payment_method_token").And.Contain("spm_test_token_123");
    }

    // ── Happy path: raw card (first-time signup charge) vaults a new token ─

    [Fact]
    public async Task ChargeAsync_WithRawCard_ReturnsVaultedPaymentMethodToken()
    {
        using var db = TestDbContextFactory.Create();
        await SeedSpreedlyCredentialAsync(db);
        await SeedProcessorCredentialAsync(db);

        var httpFactory = MakeHttpClientFactory(req => JsonResponse(HttpStatusCode.OK, new
        {
            transaction = new
            {
                token     = "txn-signup-1",
                succeeded = true,
                payment_method = new { token = "spm_new_vaulted_token" }
            }
        }), out var handler);

        var svc = new SpreedlyCardGatewayService(db, httpFactory, new FakeEncryptionService(), EmptyConfig(), Logger());
        var req = new GatewayChargeRequest
        {
            MemberId            = "member-1",
            Amount              = 49.99m,
            Currency            = "USD",
            DownstreamProcessor = CardProcessor.NmiSpreedly,
            RetainOnSuccess     = true,
            RawCard = new RawCardDetails
            {
                FirstName = "Jane",
                LastName  = "Doe",
                Number    = "4111111111111111",
                Month     = 12,
                Year      = DateTime.UtcNow.Year + 2,
                Cvv       = "123"
            }
        };

        var result = await svc.ChargeWithProcessorAsync(req, CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SpreedlyPaymentMethodToken.Should().Be("spm_new_vaulted_token");
        handler.LastRequestBody.Should().Contain("credit_card").And.Contain("4111111111111111").And.Contain("retain_on_success");
    }

    // ── Declined charge → failure with Spreedly's message ─────────────────

    [Fact]
    public async Task ChargeAsync_WhenSpreedlyDeclines_ReturnsDeclinedFailure()
    {
        using var db = TestDbContextFactory.Create();
        await SeedSpreedlyCredentialAsync(db);
        await SeedProcessorCredentialAsync(db);

        var httpFactory = MakeHttpClientFactory(req => JsonResponse(HttpStatusCode.OK, new
        {
            transaction = new
            {
                token     = "txn-declined-1",
                succeeded = false,
                message   = "Insufficient funds"
            }
        }), out _);

        var svc = new SpreedlyCardGatewayService(db, httpFactory, new FakeEncryptionService(), EmptyConfig(), Logger());
        var result = await svc.ChargeWithProcessorAsync(MakeRequest(), CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SPREEDLY_DECLINED");
        result.Error.Should().Be("Insufficient funds");
    }

    // ── Refund ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RefundAsync_WhenSpreedlyAccepts_ReturnsSuccess()
    {
        using var db = TestDbContextFactory.Create();
        await SeedSpreedlyCredentialAsync(db);
        await SeedProcessorCredentialAsync(db);

        var httpFactory = MakeHttpClientFactory(req => JsonResponse(HttpStatusCode.OK, new
        {
            transaction = new { succeeded = true }
        }), out var handler);

        var svc = new SpreedlyCardGatewayService(db, httpFactory, new FakeEncryptionService(), EmptyConfig(), Logger());
        var result = await svc.RefundWithProcessorAsync("txn-123", 50m, CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        handler.LastRequest!.RequestUri!.ToString().Should().Contain("/v1/transactions/txn-123/credit.json");
    }

    [Fact]
    public async Task RefundAsync_WhenSpreedlyCredentialMissing_ReturnsFailure()
    {
        using var db = TestDbContextFactory.Create();
        var httpFactory = MakeHttpClientFactory(_ => JsonResponse(HttpStatusCode.OK, new { }), out _);
        var svc = new SpreedlyCardGatewayService(db, httpFactory, new FakeEncryptionService(), EmptyConfig(), Logger());

        var result = await svc.RefundWithProcessorAsync("txn-123", 50m, CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SPREEDLY_CREDENTIAL_MISSING");
    }

    [Fact]
    public async Task RefundAsync_WhenSpreedlyRejects_ReturnsRefundFailedFailure()
    {
        using var db = TestDbContextFactory.Create();
        await SeedSpreedlyCredentialAsync(db);
        await SeedProcessorCredentialAsync(db);

        var httpFactory = MakeHttpClientFactory(req => JsonResponse(HttpStatusCode.OK, new
        {
            transaction = new { succeeded = false, message = "Transaction not refundable" }
        }), out _);

        var svc = new SpreedlyCardGatewayService(db, httpFactory, new FakeEncryptionService(), EmptyConfig(), Logger());
        var result = await svc.RefundWithProcessorAsync("txn-123", 50m, CardProcessor.NmiSpreedly);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SPREEDLY_REFUND_FAILED");
    }
}
