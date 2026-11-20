using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace MLMConquerorGlobalEdition.SharedAPICenter.Seguridad;

/// <summary>
/// Autenticación de <c>/api/v1/webhooks/{provider}/...</c>: firma HMAC-SHA256 del cuerpo con el
/// secreto de esa pasarela (<c>Webhooks:{provider}:Secreto</c>).
/// </summary>
/// <remarks>
/// Cabeceras: <c>X-Webhook-Timestamp</c> (segundos Unix) y <c>X-Webhook-Signature</c> =
/// <c>sha256=</c> + hex de HMAC-SHA256(secreto, <c>"{timestamp}.{cuerpo}"</c>). La marca de tiempo
/// entra en la firma y se acepta solo dentro de <see cref="Tolerancia"/>, para que una petición
/// capturada no se pueda repetir más tarde. Pasarela sin secreto configurado = nadie entra.
/// </remarks>
public sealed partial class FirmaWebhookHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration config,
    TimeProvider reloj)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Esquema = "FirmaWebhook";
    public const string CabeceraFirma = "X-Webhook-Signature";
    public const string CabeceraMarca = "X-Webhook-Timestamp";
    public static readonly TimeSpan Tolerancia = TimeSpan.FromMinutes(5);
    private const int CuerpoMaximoBytes = 64 * 1024;

    public static string ClaveDelSecreto(string pasarela) => $"Webhooks:{pasarela}:Secreto";

    [GeneratedRegex("^[a-z0-9-]{1,32}$", RegexOptions.CultureInvariant)]
    private static partial Regex PasarelaValida();

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var pasarela = (Request.RouteValues["provider"] as string ?? string.Empty).ToLowerInvariant();
        if (!PasarelaValida().IsMatch(pasarela))
            return AuthenticateResult.Fail("Pasarela no válida.");

        var secreto = config[ClaveDelSecreto(pasarela)];
        if (string.IsNullOrWhiteSpace(secreto))
            return AuthenticateResult.Fail("Pasarela sin secreto de webhook configurado.");

        if (!Request.Headers.TryGetValue(CabeceraFirma, out var firma) || firma.Count != 1
            || !Request.Headers.TryGetValue(CabeceraMarca, out var marca) || marca.Count != 1)
            return AuthenticateResult.NoResult();

        if (!long.TryParse(marca[0], NumberStyles.None, CultureInfo.InvariantCulture, out var segundos))
            return AuthenticateResult.Fail("Marca de tiempo no válida.");

        // UtcNow: la marca de un webhook es una fecha de protocolo (segundos Unix).
        var ahora = reloj.GetUtcNow().ToUnixTimeSeconds();
        if (Math.Abs(ahora - segundos) > (long)Tolerancia.TotalSeconds)
            return AuthenticateResult.Fail("Marca de tiempo fuera de la ventana.");

        var cuerpo = await LeerCuerpoAsync();
        if (cuerpo is null)
            return AuthenticateResult.Fail("Cuerpo demasiado grande.");

        if (!FirmaValida(secreto, marca[0]!, cuerpo, firma[0]))
            return AuthenticateResult.Fail("Firma no válida.");

        var identidad = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, $"pasarela:{pasarela}")], Esquema);
        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identidad), Esquema));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        RespuestaJson.EscribirAsync(Context, StatusCodes.Status401Unauthorized,
            "INVALID_SIGNATURE", "Missing or invalid webhook signature.");

    /// <summary>La firma que tiene que mandar la pasarela, en el formato de la cabecera.</summary>
    public static string Firmar(string secreto, string marca, byte[] cuerpo)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secreto));
        var prefijo = Encoding.UTF8.GetBytes(marca + ".");
        hmac.TransformBlock(prefijo, 0, prefijo.Length, null, 0);
        hmac.TransformFinalBlock(cuerpo, 0, cuerpo.Length);
        return "sha256=" + Convert.ToHexStringLower(hmac.Hash!);
    }

    internal static bool FirmaValida(string secreto, string marca, byte[] cuerpo, string? recibida)
    {
        if (string.IsNullOrEmpty(recibida)) return false;
        var esperada = Encoding.ASCII.GetBytes(Firmar(secreto, marca, cuerpo));
        var dada = Encoding.ASCII.GetBytes(recibida.Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(esperada, dada);
    }

    /// <summary>Lee el cuerpo y lo deja rebobinado para que el controlador lo vuelva a leer.</summary>
    private async Task<byte[]?> LeerCuerpoAsync()
    {
        if (Request.ContentLength > CuerpoMaximoBytes) return null;

        Request.EnableBuffering(CuerpoMaximoBytes);
        using var copia = new MemoryStream();
        var bufer = new byte[8192];
        int leidos;
        while ((leidos = await Request.Body.ReadAsync(bufer, Context.RequestAborted)) > 0)
        {
            if (copia.Length + leidos > CuerpoMaximoBytes) return null;
            copia.Write(bufer, 0, leidos);
        }
        Request.Body.Position = 0;
        return copia.ToArray();
    }
}
