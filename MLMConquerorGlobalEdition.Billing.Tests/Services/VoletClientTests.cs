using FluentAssertions;
using MLMConquerorGlobalEdition.Repository.Services.Payout.Volet;
using Xunit;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services;

/// <summary>
/// El token de Volet es la parte del contrato más fácil de romper sin darse cuenta: si el
/// formato del hash, la caja de los hex o la ventana horaria salen mal, el proveedor devuelve
/// NotAuth y no hay forma de deducir por qué desde el mensaje.
/// </summary>
public class VoletAuthTokenTests
{
    // Plantilla como la guarda MWRLife en config: el secreto y el separador los define el
    // proveedor; lo único fijo es el marcador ##datetime##.
    private const string Template = "secret-api-password:##datetime##";

    [Fact]
    public void Token_Is_Uppercase_Hex_Sha256()
    {
        var token = VoletClient.CurrentAuthToken(Template, new DateTime(2026, 8, 15, 14, 30, 0, DateTimeKind.Utc));

        // SHA-256 en hex son 64 caracteres. AdvCash los espera en MAYÚSCULA.
        token.Should().HaveLength(64);
        token.Should().MatchRegex("^[0-9A-F]{64}$");
    }

    [Fact]
    public void Token_Uses_The_Utc_Hour_Not_The_Minute()
    {
        var atHalfPast = VoletClient.CurrentAuthToken(Template, new DateTime(2026, 8, 15, 14, 30, 0, DateTimeKind.Utc));
        var atFiveTo   = VoletClient.CurrentAuthToken(Template, new DateTime(2026, 8, 15, 14, 55, 0, DateTimeKind.Utc));

        // Dentro de la misma hora el token no cambia: por eso no hace falta cachearlo.
        atHalfPast.Should().Be(atFiveTo);
    }

    [Fact]
    public void Token_Rotates_On_The_Hour()
    {
        var thisHour = VoletClient.CurrentAuthToken(Template, new DateTime(2026, 8, 15, 14, 59, 0, DateTimeKind.Utc));
        var nextHour = VoletClient.CurrentAuthToken(Template, new DateTime(2026, 8, 15, 15, 0, 0, DateTimeKind.Utc));

        thisHour.Should().NotBe(nextHour);
    }

    [Fact]
    public void Token_Matches_The_Known_Sha256_Of_The_Stamped_Template()
    {
        // Verificación independiente: se calcula el SHA-256 de "secret-api-password:20260815:14"
        // por fuera del cliente y tiene que coincidir. Si alguien cambia el formato de la
        // marca temporal, esta prueba lo detecta.
        var expected = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("secret-api-password:20260815:14")));

        VoletClient.CurrentAuthToken(Template, new DateTime(2026, 8, 15, 14, 30, 0, DateTimeKind.Utc))
            .Should().Be(expected);
    }

    [Fact]
    public void A_Template_Without_The_Placeholder_Produces_A_Static_Token()
    {
        // Caso de configuración mal cargada: sin ##datetime## el token no rota. No se
        // rechaza —el proveedor manda— pero queda documentado que es un error de carga.
        var a = VoletClient.CurrentAuthToken("no-placeholder", new DateTime(2026, 8, 15, 14, 0, 0, DateTimeKind.Utc));
        var b = VoletClient.CurrentAuthToken("no-placeholder", new DateTime(2026, 8, 15, 15, 0, 0, DateTimeKind.Utc));

        a.Should().Be(b);
    }
}
