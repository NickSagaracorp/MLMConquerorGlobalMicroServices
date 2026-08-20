using System.Globalization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Billing.Services.Payout;
using MLMConquerorGlobalEdition.Repository.Services.Payout.PayQuicker;
using Contracts = MLMConquerorGlobalEdition.Repository.Services.Payout.PayQuicker.Contracts;
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

/// <summary>
/// PayQuicker direcciona con DOS datos: programUserId (nuestro MemberId) Y email. Ambos son
/// required en el schema PortalPaymentQuote del OpenAPI.
///
/// Estas pruebas fijan que el programUserId sea SIEMPRE el MemberId y nunca el
/// AccountIdentifier de la wallet — que en PayQuicker guarda el email. Mandar el email como
/// programUserId hace que el proveedor no encuentre al usuario, y el síntoma sería un pago
/// que falla sin motivo evidente.
/// </summary>
public class PayQuickerAddressingTests
{
    private sealed class CapturingClient : IPayQuickerClient
    {
        public string Version => "V2";
        public PayQuickerAccountRequest? LastAccount { get; private set; }
        public PayQuickerPaymentRequest? LastPayment { get; private set; }
        public string? LastBalanceScope { get; private set; }

        public Task<SharedKernel.Result<PayQuickerAccountResult>> CreateInvitationAsync(
            PayQuickerAccountRequest request, PayQuickerSettings settings, CancellationToken ct = default)
        {
            LastAccount = request;
            return Task.FromResult(SharedKernel.Result<PayQuickerAccountResult>.Success(
                new PayQuickerAccountResult { Exists = true, InvitationKey = "key-1" }));
        }

        public Task<SharedKernel.Result<PayQuickerAccountResult>> GetAccountAsync(
            PayQuickerAccountRequest request, PayQuickerSettings settings, CancellationToken ct = default)
        {
            LastAccount = request;
            return Task.FromResult(SharedKernel.Result<PayQuickerAccountResult>.Success(
                new PayQuickerAccountResult { Exists = true }));
        }

        public Task<SharedKernel.Result<decimal>> GetBalanceAsync(
            string programUserId, string currency, PayQuickerSettings settings, CancellationToken ct = default)
        {
            LastBalanceScope = programUserId;
            return Task.FromResult(SharedKernel.Result<decimal>.Success(0m));
        }

        public Task<SharedKernel.Result<PayQuickerTransferResult>> SendPaymentAsync(
            PayQuickerPaymentRequest request, PayQuickerSettings settings, CancellationToken ct = default)
        {
            LastPayment = request;
            return Task.FromResult(SharedKernel.Result<PayQuickerTransferResult>.Success(
                new PayQuickerTransferResult { GatewayTransactionId = "pmnt-1" }));
        }

        public Task<SharedKernel.Result<PayQuickerTransferStatus>> GetTransferStatusAsync(
            string clientPaymentRef, PayQuickerSettings settings, CancellationToken ct = default)
            => Task.FromResult(SharedKernel.Result<PayQuickerTransferStatus>.Success(
                new PayQuickerTransferStatus { State = PayoutTransferState.Succeeded }));
    }

    private sealed class StubSettings : IPayQuickerSettingsProvider
    {
        public Task<SharedKernel.Result<PayQuickerSettings>> GetAsync(CancellationToken ct = default)
            => Task.FromResult(SharedKernel.Result<PayQuickerSettings>.Success(new PayQuickerSettings
            {
                ApiVersion = "V2", Environment = "Sandbox", BaseUrl = "https://example.test",
                ClientId = "id", ClientSecret = "secret", FundingAccountToken = "acct-1",
                ProgramToken = "prog-1", TokenUrl = "https://example.test/token", Scopes = "api"
            }));
    }

    private const string MemberId = "AMB-123456";
    private const string Email    = "member@example.com";

    private static (PayQuickerPayoutGatewayService Service, CapturingClient Client) Build()
    {
        var client = new CapturingClient();
        var service = new PayQuickerPayoutGatewayService(
            new StubSettings(), new IPayQuickerClient[] { client },
            NullLogger<PayQuickerPayoutGatewayService>.Instance);
        return (service, client);
    }

    [Fact]
    public async Task Disburse_Sends_Both_MemberId_And_Email()
    {
        var (service, client) = Build();

        await service.DisburseAsync(new PayoutTransferContext
        {
            MemberId = MemberId, AccountIdentifier = Email, AmountUsd = 10m, Reference = "attempt-1"
        });

        client.LastPayment!.ProgramUserId.Should().Be(MemberId);
        client.LastPayment!.Email.Should().Be(Email);
    }

    [Fact]
    public async Task Validate_Uses_The_MemberId_As_ProgramUserId_Not_The_Email()
    {
        var (service, client) = Build();

        // La wallet guarda el EMAIL en AccountIdentifier. Antes esto se mandaba como
        // programUserId y PayQuicker no encontraba al usuario.
        await service.ValidateAccountAsync(new PayoutAccountContext
        {
            MemberId = MemberId, WalletType = WalletType.PayQuicker, AccountIdentifier = Email
        });

        client.LastAccount!.ProgramUserId.Should().Be(MemberId);
        client.LastAccount!.ProgramUserId.Should().NotContain("@");
        client.LastAccount!.Email.Should().Be(Email);
    }

    [Fact]
    public async Task Balance_Is_Scoped_By_MemberId_Not_By_Email()
    {
        var (service, client) = Build();

        await service.GetBalanceAsync(MemberId, Email);

        client.LastBalanceScope.Should().Be(MemberId);
        client.LastBalanceScope.Should().NotContain("@");
    }

    [Fact]
    public async Task Subscribe_Sends_Both_Identifiers()
    {
        var (service, client) = Build();

        await service.SubscribeAccountAsync(new PayoutAccountContext
        {
            MemberId = MemberId, WalletType = WalletType.PayQuicker, AccountMeta = Email
        });

        client.LastAccount!.ProgramUserId.Should().Be(MemberId);
        client.LastAccount!.Email.Should().Be(Email);
    }

    [Fact]
    public async Task Disburse_Without_An_Email_Fails_Instead_Of_Guessing()
    {
        var (service, _) = Build();

        // Sin email no se puede direccionar: PayQuicker exige los dos campos.
        var result = await service.DisburseAsync(new PayoutTransferContext
        {
            MemberId = MemberId, AccountIdentifier = MemberId, AmountUsd = 10m, Reference = "attempt-1"
        });

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PAYQUICKER_EMAIL_REQUIRED");
    }
}

/// <summary>
/// PayQuicker no usa 404 para "no encontrado": responde 400 con un cuerpo
/// {"severity":"Critical","error":"NoResultFound",...}.
///
/// Distinguir eso de un fallo real es crítico: "no existe" es la respuesta esperada para un
/// miembro que todavía no fue invitado, y es justo el caso donde hay que seguir adelante y
/// crearle la invitación. Tratarlo como error corta el flujo y ese miembro nunca cobra.
/// </summary>
public class PayQuickerNotFoundTests
{
    private const string NotFoundBody = """
        {"severity":"Critical","error":"NoResultFound","code":1,
         "message":"The requested resource was not found invitation. Could not find invitation from fundingAccountPublicId acct-1 and userCompanyAssignedUniqueKey AMB-141536.",
         "referenceId":"5133f1b413a646348e8039a691721631"}
        """;

    private const string RealFailureBody = """
        {"severity":"Critical","error":"NotAuthorized","code":42,"message":"Invalid client credentials."}
        """;

    private static HttpResponseMessage Response(System.Net.HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public async Task A_NoResultFound_Body_Maps_To_The_NotFound_Code()
    {
        using var response = Response(System.Net.HttpStatusCode.BadRequest, NotFoundBody);

        var result = await PayQuickerHttp.ReadAsync<object>(response, "look up account", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(PayQuickerHttp.NotFoundErrorCode);
    }

    [Fact]
    public async Task A_Real_Failure_Does_Not_Map_To_NotFound()
    {
        using var response = Response(System.Net.HttpStatusCode.BadRequest, RealFailureBody);

        var result = await PayQuickerHttp.ReadAsync<object>(response, "look up account", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        // Un problema de credenciales NO debe confundirse con "la cuenta no existe": si se
        // confundieran, el sistema intentaría crear invitaciones contra un login inválido.
        result.ErrorCode.Should().NotBe(PayQuickerHttp.NotFoundErrorCode);
        result.ErrorCode.Should().Be("PAYQUICKER_HTTP_400");
    }

    [Fact]
    public async Task The_Provider_Message_Is_Surfaced_Instead_Of_The_Raw_Body()
    {
        using var response = Response(System.Net.HttpStatusCode.BadRequest, RealFailureBody);

        var result = await PayQuickerHttp.ReadAsync<object>(response, "send payment", CancellationToken.None);

        // Quien lee el error necesita el motivo, no un volcado de JSON.
        result.Error.Should().Contain("Invalid client credentials.");
        result.Error.Should().NotContain("severity");
    }

    [Fact]
    public async Task An_Unparseable_Error_Body_Still_Fails_Cleanly()
    {
        using var response = Response(System.Net.HttpStatusCode.BadGateway, "<html>gateway timeout</html>");

        var result = await PayQuickerHttp.ReadAsync<object>(response, "send payment", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PAYQUICKER_HTTP_502");
    }
}

/// <summary>
/// La misma cuenta de fondeo se escribe distinto en cada versión de la API de PayQuicker.
/// Cargar la forma equivocada no produce un error entendible: el lookup no valida el formato
/// y el POST de invitación responde 500 con un mensaje genérico. Estas pruebas fijan la
/// normalización para que la confusión no llegue nunca al proveedor.
/// </summary>
public class PayQuickerFundingAccountFormatTests
{
    private const string Bare   = "d2ffd5ee9d3945d7936a9648e706ccc6";
    private const string Prefixed = "acct-d2ffd5ee-9d39-45d7-936a-9648e706ccc6";

    [Theory]
    [InlineData(Bare)]
    [InlineData(Prefixed)]
    public void V1_Always_Gets_The_Bare_Guid(string input)
        => PayQuickerSettingsProvider.NormalizeFundingAccount(input, "V1").Should().Be(Bare);

    [Theory]
    [InlineData(Bare)]
    [InlineData(Prefixed)]
    public void V2_Always_Gets_The_Prefixed_Form(string input)
        => PayQuickerSettingsProvider.NormalizeFundingAccount(input, "V2").Should().Be(Prefixed);

    [Fact]
    public void Both_Forms_Describe_The_Same_Account()
    {
        // Sanity: no se esta inventando una cuenta distinta, es el mismo GUID.
        Guid.Parse(Bare).Should().Be(Guid.Parse(Prefixed["acct-".Length..]));
    }

    [Fact]
    public void A_Value_That_Is_Not_A_Guid_Is_Left_Untouched()
        // Puede ser un identificador con otra forma; no corresponde adivinar.
        => PayQuickerSettingsProvider.NormalizeFundingAccount("some-other-id", "V1")
            .Should().Be("some-other-id");

    [Fact]
    public void Whitespace_Is_Trimmed()
        // Pegar desde un panel suele arrastrar espacios.
        => PayQuickerSettingsProvider.NormalizeFundingAccount($"  {Prefixed}  ", "V1").Should().Be(Bare);

    [Fact]
    public void Null_Stays_Null()
        => PayQuickerSettingsProvider.NormalizeFundingAccount(null, "V1").Should().BeNull();
}

/// <summary>
/// v1 devuelve un ARRAY de invitaciones incluso al crear una sola. Deserializar como objeto
/// hace fallar la llamada DESPUES de que el proveedor ya creo la invitacion y mando el mail:
/// el peor tipo de error, porque el efecto ya ocurrio pero el sistema lo reporta como fallo.
/// </summary>
public class PayQuickerV1InvitationShapeTests
{
    // Respuesta real del sandbox, recortada.
    private const string CreateInvitationBody = """
        [{"dateInvited":"2026-08-19T00:15:25Z",
          "invitationKey":"k7BJ3OmFTwgBbU66nJjZtIiuk38cgFSEWmZubWEMnvySDZpeMpbj0q6XJuNLUuF2",
          "invitationStatus":"InvitationStatusType_Pending",
          "registrationStatus":"NotStarted",
          "registrationDetails":{"fundingAccountPublicId":"d2ffd5ee9d3945d7936a9648e706ccc6","issuePlasticCard":false}}]
        """;

    [Fact]
    public async Task The_Create_Invitation_Response_Deserializes_As_A_List()
    {
        using var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(CreateInvitationBody, System.Text.Encoding.UTF8, "application/json")
        };

        var result = await PayQuickerHttp.ReadAsync<List<Contracts.V1InvitationResponse>>(
            response, "create invitation", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].InvitationKey.Should().StartWith("k7BJ3OmFTwgBbU66");
        result.Value![0].InvitationStatus.Should().Be("InvitationStatusType_Pending");
        result.Value![0].RegistrationStatus.Should().Be("NotStarted");
    }

    [Fact]
    public async Task Deserializing_It_As_A_Single_Object_Fails()
    {
        // Esto es exactamente lo que hacia antes y por que fallaba.
        using var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(CreateInvitationBody, System.Text.Encoding.UTF8, "application/json")
        };

        var result = await PayQuickerHttp.ReadAsync<Contracts.V1InvitationResponse>(
            response, "create invitation", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PAYQUICKER_MALFORMED_RESPONSE");
    }
}
