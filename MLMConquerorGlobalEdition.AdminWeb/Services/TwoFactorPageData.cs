using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminWeb.Services;

/// <summary>
/// Lo que las páginas del segundo factor necesitan para pintarse y que no cabe en un formulario:
/// el canal del reto en curso y el material de enrolamiento (QR y clave compartida).
///
/// Vive fuera de <see cref="AuthEndpoints"/> a propósito. Aquello son manejadores de POST —una
/// acción del usuario que acaba en redirección—; esto se ejecuta durante el render de una
/// página, que es un GET. Meterlo allí habría obligado a inventar un endpoint intermedio cuyo
/// único trabajo sería devolverle a la página datos que la página puede pedir directamente.
///
/// Todo esto corre en el servidor: las cookies del reto son <c>HttpOnly</c>, así que el código
/// que se ejecuta en el navegador no puede leerlas. Las páginas que lo usan se renderizan en
/// modo estático (SSR), sin <c>@@rendermode</c>, igual que <c>Login.razor</c>.
/// </summary>
public sealed class TwoFactorPageData
{
    /// <summary>Nombre del claim del canal dentro del ChallengeToken (lo emite Authn).</summary>
    private const string ChannelClaim = "channel";

    private readonly IHttpClientFactory   _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<TwoFactorPageData> _logger;

    public TwoFactorPageData(
        IHttpClientFactory   httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        ILogger<TwoFactorPageData> logger)
    {
        _httpClientFactory   = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _logger              = logger;
    }

    /// <summary>
    /// Lee del propio ChallengeToken con qué canal se emitió el reto y a quién.
    /// Devuelve null si no hay cookie o no es legible: sin reto no hay pantalla que pintar.
    /// </summary>
    /// <remarks>
    /// El canal sale del token y no de la URL porque sobrevive a los rebotes de error: cuando el
    /// usuario falla el código, el manejador redirige con <c>?error=…</c> y nada más, y un canal
    /// que viajase en la query se habría perdido justo ahí. Con el canal perdido reaparecería el
    /// botón de reenviar en una pantalla de autenticador, que es exactamente lo que no debe pasar.
    ///
    /// El token NO se valida aquí, solo se lee: esto decide qué frase se enseña, no qué se deja
    /// hacer. La firma la comprueba la API cuando el reto se canjea, que es donde importa.
    /// </remarks>
    public ChallengeDisplay? ReadChallenge()
    {
        var token = ChallengeCookies.Read(_httpContextAccessor.HttpContext, ChallengeCookies.Login);
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
            return null;

        try
        {
            var jwt     = handler.ReadJwtToken(token);
            var channel = jwt.Claims.FirstOrDefault(c => c.Type == ChannelClaim)?.Value;
            var email   = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value;

            // El destino que devolvió la API viaja por query string y llega ya enmascarado; solo
            // está en la primera carga y tras un reenvío. Cuando falta —al volver de un error—
            // el correo del propio reto permite reconstruirlo sin preguntarle nada a nadie.
            return new ChallengeDisplay(channel, MaskEmail(email));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "El ChallengeToken de la cookie no se pudo leer.");
            return null;
        }
    }

    /// <summary>
    /// Abre el enrolamiento contra la API y devuelve el QR y la clave compartida.
    /// Devuelve null si no hay cookie de enrolamiento o si la API la rechaza: en ambos casos no
    /// hay nada que enseñar y la página debe mandar al usuario de vuelta al login.
    /// </summary>
    public async Task<EnrollmentMaterial?> BeginEnrollmentAsync(CancellationToken ct = default)
    {
        var enrollmentToken = ChallengeCookies.Read(
            _httpContextAccessor.HttpContext, ChallengeCookies.Enrollment);

        if (string.IsNullOrWhiteSpace(enrollmentToken))
            return null;

        try
        {
            var httpClient = _httpClientFactory.CreateClient("AuthApi");
            var response   = await httpClient.PostAsJsonAsync(
                "api/v1/auth/two-factor/enroll/begin",
                new { EnrollmentToken = enrollmentToken }, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            var apiResponse = await response.Content
                .ReadFromJsonAsync<ApiResponse<EnrollmentMaterial>>(cancellationToken: ct);

            if (apiResponse?.Success != true || apiResponse.Data is null)
                return null;

            return string.IsNullOrWhiteSpace(apiResponse.Data.QrCodePngDataUri)
                ? null
                : apiResponse.Data;
        }
        catch (Exception ex)
        {
            // Un fallo de red aquí es indistinguible, para el usuario, de un reto caducado: en
            // los dos casos esta página no puede pintarse y lo único útil es volver al login.
            _logger.LogWarning(ex, "No se pudo abrir el enrolamiento contra la API de autenticación.");
            return null;
        }
    }

    /// <summary>Enmascara un correo dejando solo la primera letra: <c>n****@@dominio.com</c>.</summary>
    private static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return string.Empty;

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0) return string.Empty;

        var local   = email[..atIndex];
        var domain  = email[(atIndex + 1)..];
        var visible = local.Length <= 1 ? local : local[..1];

        return $"{visible}{new string('*', Math.Max(1, local.Length - 1))}@{domain}";
    }

    /// <summary>Lo que la pantalla de verificación necesita saber del reto en curso.</summary>
    public sealed record ChallengeDisplay(string? Channel, string MaskedEmail);

    /// <summary>Respuesta de <c>two-factor/enroll/begin</c>, recortada a lo que se pinta.</summary>
    public sealed record EnrollmentMaterial
    {
        public string SharedKey        { get; init; } = string.Empty;
        public string AuthenticatorUri { get; init; } = string.Empty;
        public string QrCodePngDataUri { get; init; } = string.Empty;
    }
}
