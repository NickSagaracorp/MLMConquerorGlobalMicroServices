using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Services;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Services;

/// <summary>
/// Pruebas del servicio real, sin mocks.
///
/// Los tres archivos de pruebas de 2FA (LoginHandlerTests, VerifyTwoFactorHandlerTests y
/// ResendTwoFactorHandlerTests) mockean ITwoFactorChallengeService, así que ninguno ejercita
/// la emisión y validación de verdad. Por eso pasó desapercibido que el servicio no podía
/// validar el challenge que él mismo emitía: el handler de JWT renombra "sub" y "email" a
/// URIs de WS-Federation, y las lecturas por nombre corto devolvían null.
/// </summary>
public class TwoFactorChallengeServiceTests
{
    /// <summary>
    /// El reloj no se fija a una fecha inventada, y no por comodidad: este servicio emite
    /// con el IDateTimeProvider inyectado pero delega la comprobación de vigencia en el
    /// handler de JWT, que usa el reloj de la máquina. Con un reloj inyectado distinto del
    /// real, todo challenge sale "expirado" o "aún no válido" y las pruebas medirían esa
    /// discrepancia en vez de lo que dicen medir.
    ///
    /// La librería Authn que sustituye a este servicio sí valida la vigencia contra el reloj
    /// inyectado, así que allí sí puede fijarse.
    /// </summary>
    private static DateTime Now() => DateTime.UtcNow;

    private static IConfiguration BuildConfig(int challengeLifetimeMinutes = 5, int resendGraceMinutes = 30)
    {
        using var rsa = RSA.Create(2048);

        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:PrivateKeyBase64"] = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()),
                ["Jwt:PublicKeyBase64"]  = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()),
                ["Jwt:Issuer"]           = "MLMConquerorGlobalEdition",
                ["Jwt:Audience"]         = "MLMConquerorGlobalEdition.Clients",
                ["Auth:TwoFactor:ChallengeLifetimeMinutes"] = challengeLifetimeMinutes.ToString(),
                ["Auth:TwoFactor:ResendGraceWindowMinutes"] = resendGraceMinutes.ToString()
            })
            .Build();
    }

    private static TwoFactorChallengeService BuildService(IConfiguration? config = null)
    {
        var dateTime = new Mock<IDateTimeProvider>();
        dateTime.Setup(d => d.Now).Returns(Now);
        return new TwoFactorChallengeService(config ?? BuildConfig(), dateTime.Object);
    }

    /// <summary>
    /// La prueba que faltaba. Antes del arreglo de MapInboundClaims esto fallaba con
    /// INVALID_CHALLENGE "Challenge token is malformed": el servicio rechazaba su propio
    /// challenge porque leía "sub" y "email" por su nombre corto y el handler los había
    /// renombrado.
    /// </summary>
    [Fact]
    public void IssueChallenge_ThenValidate_RoundTripsSubjectAndEmail()
    {
        var service = BuildService();

        var code     = service.GenerateCode();
        var codeHash = service.HashCode(code);
        var token    = service.IssueChallenge("user-123", "usuario@ejemplo.com", codeHash);

        var result = service.ValidateChallenge(token);

        result.IsSuccess.Should().BeTrue(
            "el servicio debe poder validar el challenge que él mismo emitió");
        result.Value!.UserId.Should().Be("user-123");
        result.Value.Email.Should().Be("usuario@ejemplo.com");
        result.Value.CodeHash.Should().Be(codeHash);
    }

    [Fact]
    public void GenerateCode_ReturnsSixDigits()
    {
        var service = BuildService();

        var code = service.GenerateCode();

        code.Should().MatchRegex("^[0-9]{6}$");
    }

    [Fact]
    public void HashCode_IsDeterministic()
    {
        var service = BuildService();

        service.HashCode("123456").Should().Be(service.HashCode("123456"));
        service.HashCode("123456").Should().NotBe(service.HashCode("654321"));
    }

    [Fact]
    public void ValidateChallenge_WhenTokenTampered_Fails()
    {
        var service = BuildService();
        var token   = service.IssueChallenge("user-123", "usuario@ejemplo.com", service.HashCode("123456"));

        // Altera el último carácter de la firma
        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');

        service.ValidateChallenge(tampered).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void ValidateChallenge_WhenSignedByAnotherKey_Fails()
    {
        var token = BuildService().IssueChallenge("user-123", "usuario@ejemplo.com", "hash");

        // Otro servicio, otro par de llaves
        var otherService = BuildService(BuildConfig());

        otherService.ValidateChallenge(token).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void ValidateChallenge_WhenEmpty_ReturnsInvalidChallenge()
    {
        var result = BuildService().ValidateChallenge("");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CHALLENGE");
    }

    [Fact]
    public void MaskEmail_ShowsOnlyFirstCharacterOfLocalPart()
    {
        BuildService().MaskEmail("usuario@ejemplo.com").Should().Be("u******@ejemplo.com");
    }
}
