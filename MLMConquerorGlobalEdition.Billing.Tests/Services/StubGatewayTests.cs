using FluentAssertions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services.CardGateway;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services;

public class StubGatewayTests
{
    private static GatewayChargeRequest MakeRequest() => new()
    {
        MemberId             = "member-1",
        Amount               = 99m,
        Currency             = "USD",
        Description          = "Test",
        TokenizedCardRef     = "tok_abc",
        NetworkTransactionId = "ntxn_abc",
        IsRecurring          = true
    };

    // ── Missing credential → failure ──────────────────────────────────────

    [Fact]
    public async Task ChargeAsync_WhenCredentialMissing_ReturnsCredentialNotFoundFailure()
    {
        using var db = TestDbContextFactory.Create();
        // No credentials seeded
        var logger = new Mock<ILogger<NmiSpreedlyGatewayService>>().Object;
        var svc = new NmiSpreedlyGatewayService(db, logger);

        var result = await svc.ChargeAsync(MakeRequest());

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CREDENTIAL_NOT_FOUND");
    }

    // ── Inactive credential → failure ─────────────────────────────────────

    [Fact]
    public async Task ChargeAsync_WhenCredentialInactive_ReturnsCredentialInactiveFailure()
    {
        using var db = TestDbContextFactory.Create();
        var cred = new ApiCredential
        {
            ServiceKey = "NmiSpreedly",
            IsActive   = false,
            CreationDate = DateTime.UtcNow,
            CreatedBy    = "test"
        };
        cred.ApiKeyEncrypted = "ENC:dummykey";
        db.ApiCredentials.Add(cred);
        await db.SaveChangesAsync();

        var logger = new Mock<ILogger<NmiSpreedlyGatewayService>>().Object;
        var svc = new NmiSpreedlyGatewayService(db, logger);

        var result = await svc.ChargeAsync(MakeRequest());

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CREDENTIAL_INACTIVE");
    }

    // ── Missing API key on otherwise active credential → failure ──────────

    [Fact]
    public async Task ChargeAsync_WhenApiKeyMissing_ReturnsCredentialIncompleteFailure()
    {
        using var db = TestDbContextFactory.Create();
        var cred = new ApiCredential
        {
            ServiceKey   = "NmiSpreedly",
            IsActive     = true,
            CreationDate = DateTime.UtcNow,
            CreatedBy    = "test"
            // ApiKeyEncrypted intentionally not set
        };
        db.ApiCredentials.Add(cred);
        await db.SaveChangesAsync();

        var logger = new Mock<ILogger<NmiSpreedlyGatewayService>>().Object;
        var svc = new NmiSpreedlyGatewayService(db, logger);

        var result = await svc.ChargeAsync(MakeRequest());

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CREDENTIAL_INCOMPLETE");
    }

    // ── Valid credential → simulated success ─────────────────────────────

    [Fact]
    public async Task ChargeAsync_WhenCredentialValid_ReturnsSimulatedSuccess()
    {
        using var db = TestDbContextFactory.Create();
        var cred = new ApiCredential
        {
            ServiceKey   = "NmiSpreedly",
            IsActive     = true,
            CreationDate = DateTime.UtcNow,
            CreatedBy    = "test"
        };
        cred.ApiKeyEncrypted = "ENC:live-api-key";
        db.ApiCredentials.Add(cred);
        await db.SaveChangesAsync();

        var logger = new Mock<ILogger<NmiSpreedlyGatewayService>>().Object;
        var svc = new NmiSpreedlyGatewayService(db, logger);

        var result = await svc.ChargeAsync(MakeRequest());

        result.IsSuccess.Should().BeTrue();
        result.Value!.GatewayTransactionId.Should().StartWith("STUB-NmiSpreedly-");
        result.Value.Status.Should().Be("simulated_success");
    }

    // ── Processor property ────────────────────────────────────────────────

    [Fact]
    public void Processor_ForNmiSpreedlyService_IsNmiSpreedly()
    {
        using var db = TestDbContextFactory.Create();
        var logger = new Mock<ILogger<NmiSpreedlyGatewayService>>().Object;
        var svc = new NmiSpreedlyGatewayService(db, logger);
        svc.Processor.Should().Be(CardProcessor.NmiSpreedly);
    }

    // ── Refund: valid credential → success ────────────────────────────────

    [Fact]
    public async Task RefundAsync_WhenCredentialValid_ReturnsSuccess()
    {
        using var db = TestDbContextFactory.Create();
        var cred = new ApiCredential
        {
            ServiceKey   = "NmiSpreedly",
            IsActive     = true,
            CreationDate = DateTime.UtcNow,
            CreatedBy    = "test"
        };
        cred.ApiKeyEncrypted = "ENC:live-api-key";
        db.ApiCredentials.Add(cred);
        await db.SaveChangesAsync();

        var logger = new Mock<ILogger<NmiSpreedlyGatewayService>>().Object;
        var svc = new NmiSpreedlyGatewayService(db, logger);

        var result = await svc.RefundAsync("txn-123", 50m);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }
}
