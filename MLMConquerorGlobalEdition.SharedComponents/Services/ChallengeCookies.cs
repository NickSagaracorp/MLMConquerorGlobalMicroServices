using Microsoft.AspNetCore.Http;

namespace MLMConquerorGlobalEdition.SharedComponents.Services;

/// <summary>
/// Las cookies de corta vida que guardan un reto entre el POST que lo emite y el POST que lo
/// canjea, con las mismas opciones en todos los casos.
///
/// Van en cookie y no en la URL por lo mismo que explicaba <c>AuthEndpoints</c> del reto del
/// segundo factor: en la URL quedarían en el historial del navegador, en los registros del proxy y
/// en la cabecera Referer de cualquier recurso externo que cargue la página.
///
/// Están aquí y no repartidas entre <c>AuthEndpoints</c> y <see cref="AccountEndpoints"/> porque
/// las dos áreas emiten retos y los dos ficheros necesitaban exactamente las mismas opciones de
/// cookie. Un segundo juego de opciones en otro archivo es de la clase de duplicado que se
/// desincroniza en silencio: basta que uno pierda <c>Secure</c> para que el reto viaje en claro sin
/// que nada falle a la vista.
/// </summary>
/// <remarks>
/// PENDIENTE: los nombres siguen siendo literales <c>mlm_admin_*</c> aunque la clase ya sea
/// compartida. No se parametrizaron aquí porque el otro usuario de estas constantes es
/// <c>AuthEndpoints</c>, que se quedó en AdminWeb y las consume desde manejadores estáticos; hacerlo
/// bien exige tocar aquel archivo, que no entraba en este movimiento de código. Mientras los dos
/// portales no compartan dominio no hay choque —cada uno sirve sus cookies desde su propio host—,
/// pero con <c>Path = "/"</c> y un mismo dominio para <c>/admin</c> y el centro de negocios, un alta
/// de teléfono a medias en un portal pisaría la del otro.
/// </remarks>
public static class ChallengeCookies
{
    /// <summary>ChallengeToken del segundo factor del login.</summary>
    public const string Login = "mlm_admin_2fa_challenge";

    /// <summary>
    /// EnrollmentToken. Deliberadamente distinta de <see cref="Login"/>: son propósitos distintos
    /// y compartir nombre invita a redimir uno donde va el otro.
    /// </summary>
    public const string Enrollment = "mlm_admin_2fa_enrollment";

    /// <summary>
    /// ChallengeToken del alta de teléfono, que se canjea en <c>/account/phone/verify</c>. Tampoco
    /// comparte nombre con las otras dos: un usuario puede tener un alta de teléfono a medias y
    /// abrir el login en otra pestaña, y ahí las dos cookies conviven.
    /// </summary>
    public const string Phone = "mlm_admin_phone_challenge";

    /// <summary>Ventana de vida. El reto de la API dura menos; esto es el techo.</summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>Mismas opciones que la cookie de sesión del portal, con vida corta.</summary>
    public static void Set(HttpContext httpContext, string name, string value) =>
        httpContext.Response.Cookies.Append(name, value, new CookieOptions
        {
            HttpOnly    = true,
            Secure      = true,
            SameSite    = SameSiteMode.Strict,
            IsEssential = true,
            Path        = "/",
            Expires     = DateTimeOffset.UtcNow.Add(Lifetime)
        });

    public static void Delete(HttpContext httpContext, string name) =>
        httpContext.Response.Cookies.Delete(name, new CookieOptions
        {
            HttpOnly = true,
            Secure   = true,
            SameSite = SameSiteMode.Strict,
            Path     = "/"
        });

    public static string? Read(HttpContext? httpContext, string name) =>
        httpContext?.Request.Cookies[name];
}
