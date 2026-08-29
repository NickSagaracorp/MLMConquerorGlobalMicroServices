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
/// Los NOMBRES no están aquí: son de cada portal y viajan en <see cref="ChallengeCookieNames"/>,
/// que se inyecta. Con <c>Path = "/"</c> y un mismo dominio para <c>/admin</c> y el centro de
/// negocios, dos portales con los mismos nombres se pisarían los retos; y dos portales con nombres
/// distintos escritos a fuego en sitios distintos —que es lo que había— escriben con un nombre y
/// leen con otro sin que nada falle a la vista.
/// </remarks>
public static class ChallengeCookies
{
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
