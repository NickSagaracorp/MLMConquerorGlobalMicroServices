using System.Globalization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Billing.Services.Payout;
using MLMConquerorGlobalEdition.Repository.Services.Payout.PayQuicker;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using Xunit;
using MLMConquerorGlobalEdition.Repository.Services.Payout;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services;

/// <summary>Cifrado de juguete para las pruebas: sólo agrega y quita el prefijo ENC:.</summary>
internal sealed class PassthroughEncryption : IEncryptionService
{
    public string Encrypt(string plaintext) => "ENC:" + plaintext;

    public string Decrypt(string ciphertext) =>
        ciphertext.StartsWith("ENC:", StringComparison.Ordinal)
            ? ciphertext["ENC:".Length..]
            : throw new InvalidOperationException("Value is not encrypted.");
}

public class PayQuickerSettingsProviderTests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("pq-" + Guid.NewGuid().ToString("N"))
            .Options);

    private static void Seed(
        AppDbContext db,
        string? apiVersion = "V2",
        string? environment = "Sandbox",
        string credentialServiceKey = "PayQuickerV2",
        string credentialEnvironment = "Sandbox",
        bool credentialActive = true,
        bool withSecrets = true)
    {
        db.PaymentGateways.Add(new PaymentGatewayInfo
        {
            Id = 5,
            WalletType = WalletType.PayQuicker,
            DisplayName = "PayQuicker",
            Description = "…",
            Currency = "USD",
            ApiVersion = apiVersion,
            Environment = environment,
            CreatedBy = "test",
            CreationDate = DateTime.UtcNow
        });

        var cred = new ApiCredential
        {
            ServiceKey = credentialServiceKey,
            Environment = credentialEnvironment,
            BaseUrl = "https://api.sandbox.payquicker.io/api/v2",
            IsActive = credentialActive,
            CreatedBy = "test",
            CreationDate = DateTime.UtcNow
        };

        if (withSecrets)
        {
            cred.ApiKeyEncrypted = "ENC:client-id";
            cred.SecretKeyEncrypted = "ENC:client-secret";
            cred.MerchantIdEncrypted = "ENC:acct-1111";
            cred.AdditionalSecretEncrypted = "ENC:prog-2222";
        }

        db.ApiCredentials.Add(cred);
        db.SaveChanges();
    }

    private static PayQuickerSettingsProvider Provider(AppDbContext db) =>
        new(db, new PassthroughEncryption());

    [Fact]
    public async Task Resolves_V2_Settings_From_Selectors()
    {
        using var db = NewDb();
        Seed(db);

        var result = await Provider(db).GetAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value!.ApiVersion.Should().Be("V2");
        result.Value.Environment.Should().Be("Sandbox");
        result.Value.ClientId.Should().Be("client-id");
        result.Value.ClientSecret.Should().Be("client-secret");
        result.Value.FundingAccountToken.Should().Be("acct-1111");
        result.Value.ProgramToken.Should().Be("prog-2222");
        result.Value.TokenUrl.Should().EndWith("/auth/connect/token");
        result.Value.Scopes.Should().Be("api readonly modify");
    }

    [Fact]
    public async Task Selecting_V1_Uses_The_V1_Credential_And_Scopes()
    {
        using var db = NewDb();
        Seed(db, apiVersion: "V1", credentialServiceKey: "PayQuickerV1");

        var result = await Provider(db).GetAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value!.ApiVersion.Should().Be("V1");
        // Los scopes de v1 NO son los de v2: usarlos cruzados devuelve invalid_scope.
        result.Value.Scopes.Should().Contain("useraccount_payment");
        result.Value.TokenUrl.Should().EndWith("/core/connect/token");
    }

    [Fact]
    public async Task Missing_Credential_For_The_Selected_Environment_Fails_Clearly()
    {
        using var db = NewDb();
        // El gateway apunta a Production pero sólo existe la credencial de Sandbox.
        Seed(db, environment: "Production", credentialEnvironment: "Sandbox");

        var result = await Provider(db).GetAsync();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PAYQUICKER_NO_CREDENTIAL");
    }

    [Fact]
    public async Task Inactive_Credential_Fails()
    {
        using var db = NewDb();
        Seed(db, credentialActive: false);

        var result = await Provider(db).GetAsync();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PAYQUICKER_CREDENTIAL_INACTIVE");
    }

    [Fact]
    public async Task Credential_Without_Secrets_Fails_Before_Any_Http_Call()
    {
        using var db = NewDb();
        Seed(db, withSecrets: false);

        var result = await Provider(db).GetAsync();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PAYQUICKER_INCOMPLETE_CREDENTIAL");
    }

    [Fact]
    public async Task Unsupported_Version_Is_Rejected()
    {
        using var db = NewDb();
        Seed(db, apiVersion: "V3");

        var result = await Provider(db).GetAsync();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PAYQUICKER_BAD_VERSION");
    }

    [Fact]
    public async Task No_Gateway_Row_Fails()
    {
        using var db = NewDb();

        var result = await Provider(db).GetAsync();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PAYQUICKER_NOT_CONFIGURED");
    }
}

public class PayQuickerHttpTests
{
    [Theory]
    [InlineData(5.02, "5.02")]
    [InlineData(5, "5.00")]
    [InlineData(0.5, "0.50")]
    [InlineData(1234.5, "1234.50")]
    public void FormatAmount_Always_Uses_Two_Decimals(decimal input, string expected)
        => PayQuickerHttp.FormatAmount(input).Should().Be(expected);

    [Fact]
    public void FormatAmount_Ignores_The_Ambient_Culture()
    {
        // En es-ES el separador decimal es la coma. Si el monto saliera "5,02" PayQuicker
        // lo rechaza — por eso el formateo fuerza cultura invariante.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("es-ES");
            PayQuickerHttp.FormatAmount(5.02m).Should().Be("5.02");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void FormatAmount_Drops_Trailing_Precision()
        // Un decimal 5.020000m conserva la escala; ToString() a secas daría "5.020000".
        => PayQuickerHttp.FormatAmount(5.020000m).Should().Be("5.02");
}

public class PayQuickerPayoutGatewayServiceTests
{
    private sealed class FakeSettings : IPayQuickerSettingsProvider
    {
        private readonly string _version;
        public FakeSettings(string version) => _version = version;

        public Task<SharedKernel.Result<PayQuickerSettings>> GetAsync(CancellationToken ct = default)
            => Task.FromResult(SharedKernel.Result<PayQuickerSettings>.Success(new PayQuickerSettings
            {
                ApiVersion = _version,
                Environment = "Sandbox",
                BaseUrl = "https://example.test",
                ClientId = "id",
                ClientSecret = "secret",
                TokenUrl = "https://example.test/token",
                Scopes = "api"
            }));
    }

    [Fact]
    public void GatewayType_Is_PayQuicker()
    {
        var svc = new PayQuickerPayoutGatewayService(
            new FakeSettings("V2"),
            Array.Empty<IPayQuickerClient>(),
            NullLogger<PayQuickerPayoutGatewayService>.Instance);

        svc.GatewayType.Should().Be(WalletType.PayQuicker);
    }

    [Fact]
    public async Task Disburse_Without_A_Client_For_The_Selected_Version_Fails_Cleanly()
    {
        var svc = new PayQuickerPayoutGatewayService(
            new FakeSettings("V2"),
            Array.Empty<IPayQuickerClient>(),   // ningún cliente registrado
            NullLogger<PayQuickerPayoutGatewayService>.Instance);

        var result = await svc.DisburseAsync(new PayoutTransferContext
        {
            MemberId = "AMB-1",
            AccountIdentifier = "member@example.com",
            AmountUsd = 10m,
            Reference = "attempt-1"
        });

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PAYQUICKER_NO_CLIENT");
    }

    [Fact]
    public async Task GetTransferStatus_Never_Reports_Failure_When_Config_Is_Broken()
    {
        // Regla de seguridad del dinero: si no se puede consultar, el estado es Unknown y el
        // intento queda Pending. Reportar Failed liberaría comisiones de un pago que quizá salió.
        var svc = new PayQuickerPayoutGatewayService(
            new FakeSettings("V2"),
            Array.Empty<IPayQuickerClient>(),
            NullLogger<PayQuickerPayoutGatewayService>.Instance);

        var result = await svc.GetTransferStatusAsync("attempt-1");

        result.IsSuccess.Should().BeTrue();
        result.Value!.State.Should().Be(PayoutTransferState.Unknown);
    }
}
