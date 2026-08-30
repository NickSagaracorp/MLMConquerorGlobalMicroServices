using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using MLMConquerorGlobalEdition.ClientCore;
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
/// <c>access_token</c>. Ese JWT dura lo que diga <c>Jwt:AccessTokenExpiryMinutes</c> de SignupAPI.
/// En cuanto caduca, la cookie es un envoltorio sin nada dentro — el usuario sigue "autenticado"
/// para ASP.NET Core y no lo está para ninguna API.
///
/// LO QUE CAMBIÓ: antes eso ERA el final, porque el refresh token que devuelve la API no se
/// guardaba en ninguna parte y no había con qué renovar. Ahora se guarda —claim
/// <see cref="RefreshTokenClaim"/>, en la misma cookie— y caducar es solo el momento de intentar la
/// renovación. Quien la intenta es <see cref="PortalSessionTokens"/>; todo lo de este archivo es lo
/// que pasa cuando esa renovación NO sale: refresh caducado, revocado o ausente. Es decir, sigue
/// siendo el final del camino, pero ahora hay un camino antes.
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
    /// El claim donde <c>AuthEndpoints</c> guarda el refresh token, al lado del de acceso.
    /// </summary>
    /// <remarks>
    /// VA DONDE VA EL DE ACCESO Y EN NINGÚN OTRO SITIO. Es una credencial de larga vida —treinta
    /// días— y la cookie de sesión de los dos portales es <c>HttpOnly</c>, <c>Secure</c> y
    /// <c>SameSite=Strict</c>: no la lee JavaScript, no sale sin TLS y no viaja en peticiones de
    /// otro sitio. Nunca en la URL, ni en el cuerpo de una página, ni en el almacenamiento del
    /// navegador, que son los tres sitios donde sí se podría robar.
    /// </remarks>
    public const string RefreshTokenClaim = "refresh_token";

    /// <summary>
    /// El claim que nombra esta sesión del portal. No es una credencial: no abre nada, solo dice qué
    /// entrada de <see cref="PortalSessionTokens"/> le corresponde a este usuario.
    /// </summary>
    /// <remarks>
    /// Hace falta porque el circuito y la petición siguiente son dos mundos distintos —ámbitos de DI
    /// distintos, incluso momentos distintos— y necesitan poder señalar la misma pareja de tokens.
    /// Lo único que comparten es la cookie, así que la identidad de la sesión viaja en ella.
    /// </remarks>
    public const string SessionIdClaim = "portal_session";

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

    /// <summary>
    /// Vuelve a firmar la cookie de sesión con una pareja de tokens nueva, dejando intacto todo lo
    /// demás que lleva dentro (identidad, roles, correo, idioma).
    /// </summary>
    /// <remarks>
    /// ES LA SEGUNDA MITAD DE LA RENOVACIÓN. La primera —conseguir los tokens nuevos— puede ocurrir
    /// en cualquier parte, circuito incluido. Esta solo puede ocurrir sobre una respuesta que
    /// todavía no haya empezado, y por eso la hace el middleware en la siguiente navegación y no el
    /// manejador dentro del circuito. Quien llame tiene que haber comprobado
    /// <c>!Response.HasStarted</c>.
    ///
    /// El principal se reconstruye entero en vez de mutarlo: un <c>ClaimsIdentity</c> ya emitido
    /// puede venir de sitios que no admiten borrar claims, y sustituir dos claims sobre una lista
    /// nueva no tiene ese problema. También se actualiza <c>httpContext.User</c>, y eso NO es
    /// cosmético: el apretón de manos del circuito de Blazor se lleva el principal de ESTA petición,
    /// así que sin esa línea el circuito recién abierto arrancaría con el token viejo.
    /// </remarks>
    public static async Task ReissueCookieAsync(HttpContext httpContext, SessionTokens tokens)
    {
        var identity = httpContext.User.Identity as ClaimsIdentity;

        var claims = httpContext.User.Claims
            .Where(c => c.Type != AccessTokenClaim && c.Type != RefreshTokenClaim)
            .ToList();

        claims.Add(new Claim(AccessTokenClaim, tokens.AccessToken));
        if (!string.IsNullOrEmpty(tokens.RefreshToken))
            claims.Add(new Claim(RefreshTokenClaim, tokens.RefreshToken));

        var renovada = new ClaimsIdentity(
            claims,
            identity?.AuthenticationType ?? CookieAuthenticationDefaults.AuthenticationScheme,
            identity?.NameClaimType ?? ClaimsIdentity.DefaultNameClaimType,
            identity?.RoleClaimType ?? ClaimsIdentity.DefaultRoleClaimType);

        var principal = new ClaimsPrincipal(renovada);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });

        httpContext.User = principal;
    }
}
