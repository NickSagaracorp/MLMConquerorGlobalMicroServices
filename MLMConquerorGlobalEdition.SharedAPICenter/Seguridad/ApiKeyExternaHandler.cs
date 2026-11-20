using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace MLMConquerorGlobalEdition.SharedAPICenter.Seguridad;

/// <summary>
/// Autenticación de <c>/api/v1/external</c>: la cabecera <c>X-Api-Key</c> contra
/// <c>ExternalApi:ApiKey</c>, comparada en tiempo constante. Sin llave configurada nadie entra.
/// </summary>
public sealed class ApiKeyExternaHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration config)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Esquema = "ApiKeyExterna";
    public const string Cabecera = "X-Api-Key";
    public const string ClaveDeConfiguracion = "ExternalApi:ApiKey";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var esperada = config[ClaveDeConfiguracion];
        if (string.IsNullOrWhiteSpace(esperada))
            return Task.FromResult(AuthenticateResult.Fail("API key externa no configurada."));

        if (!Request.Headers.TryGetValue(Cabecera, out var recibida) || recibida.Count != 1)
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!IgualesEnTiempoConstante(recibida[0], esperada))
            return Task.FromResult(AuthenticateResult.Fail("API key externa no válida."));

        var identidad = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "sistema-externo")], Esquema);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identidad), Esquema)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        RespuestaJson.EscribirAsync(Context, StatusCodes.Status401Unauthorized,
            "UNAUTHORIZED", "Invalid or missing API key.");

    internal static bool IgualesEnTiempoConstante(string? a, string b)
    {
        if (a is null) return false;
        var x = SHA256.HashData(Encoding.UTF8.GetBytes(a));
        var y = SHA256.HashData(Encoding.UTF8.GetBytes(b));
        return CryptographicOperations.FixedTimeEquals(x, y);
    }
}
