using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using MLMConquerorGlobalEdition.SharedComponents.Resources;

namespace MLMConquerorGlobalEdition.SharedComponents.Server.Services;

/// <summary>
/// Lo que significa "esta sesión ya no vale" y qué se hace con ella, en un solo sitio.
///
/// Lo comparten las tres piezas que pueden descubrirlo: el middleware que mira cada navegación
/// (<see cref="SessionExpiryMiddleware"/>), el manejador que lleva el token a las APIs
/// (<see cref="ApiAuthHandler"/>) y la salida de la puerta (<see cref="AuthEndpoints.LogoutAsync"/>).
/// </summary>
/// <remarks>
/// POR QUÉ CADUCA LA SESIÓN AUNQUE LA COOKIE SIGA VIVA: la cookie del portal dura horas (8 en
/// administración, 24 en el centro de negocios) y lleva dentro el JWT como claim
/// <c>access_token</c>. Ese JWT dura lo que diga <c>Jwt:AccessTokenExpiryMinutes</c> de SignupAPI, y
/// NO se renueva: el refresh token que devuelve la API no se guarda en ninguna parte. Así que en
/// cuanto el JWT caduca la cookie es un envoltorio sin nada dentro — el usuario sigue "autenticado"
/// para ASP.NET Core y no lo está para ninguna API. De ahí que caducar signifique cerrar la sesión,
/// y no solo enseñar un aviso.
/// </remarks>
public static class SessionExpiry
{
    /// <summary>
    /// El código con el que se le dice a la pantalla de login por qué está ahí el usuario. Las dos
    /// pantallas ya lo traducen; el mapa completo de códigos vive en
    /// <c>SharedComponents.Resources.LoginErrorMessages</c>.
    /// </summary>
    public const string ErrorCode = LoginErrorMessages.SessionExpired;

    /// <summary>
    /// El motivo con el que el circuito llama a la salida. Va como <c>?reason=</c> y no como
    /// <c>?error=</c> a propósito: la salida no está fallando, está cerrando una sesión muerta, y
    /// quien traduce el código es la pantalla de login a la que la salida redirige después.
    /// </summary>
    public const string ReasonQueryParam = "reason";

    /// <summary>El claim donde <c>AuthEndpoints</c> guarda el JWT dentro de la cookie de sesión.</summary>
    public const string AccessTokenClaim = "access_token";

    /// <summary>
    /// Margen con el que se considera caducado un token que aún no lo está. Evita gastar una
    /// llamada que va a volver con 401 por unos segundos de diferencia de reloj.
    /// </summary>
    private static readonly TimeSpan Skew = TimeSpan.FromSeconds(5);

    /// <summary>
    /// ¿Este JWT ya no vale? Un token ilegible cuenta como caducado: si no se puede leer, tampoco se
    /// puede confiar en él.
    /// </summary>
    /// <remarks>
    /// <c>UtcNow</c> y no <c>Now</c>: el <c>exp</c> de un JWT es una fecha de protocolo, definida en
    /// UTC por el RFC, y compararla contra la hora del servidor daría por caducada media jornada de
    /// sesiones en cuanto el servidor no esté en UTC.
    /// </remarks>
    public static bool IsExpired(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return true;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token)) return true;
            return handler.ReadJwtToken(token).ValidTo <= DateTime.UtcNow.Add(Skew);
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// La URL de la pantalla de login con el aviso puesto.
    /// </summary>
    public static string LoginUrl(string loginPage) =>
        $"{loginPage}{(loginPage.Contains('?') ? '&' : '?')}error={ErrorCode}";

    /// <summary>
    /// La URL de la salida del portal con el motivo puesto. Es a donde manda el circuito: pasar por
    /// la salida es la ÚNICA forma de que la cookie se limpie de verdad, porque desde dentro del
    /// circuito la respuesta HTTP que hay a mano es la del WebSocket y ya empezó.
    /// </summary>
    public static string LogoutUrl(string logoutRoute) =>
        $"{logoutRoute}{(logoutRoute.Contains('?') ? '&' : '?')}{ReasonQueryParam}={ErrorCode}";

    /// <summary>
    /// Cierra la sesión y manda al login con el aviso, sobre una respuesta HTTP que todavía no ha
    /// empezado. Quien llame tiene que haberlo comprobado.
    /// </summary>
    public static async Task SignOutAndRedirectAsync(HttpContext httpContext, string loginPage)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        httpContext.Response.Redirect(LoginUrl(loginPage));
    }
}
