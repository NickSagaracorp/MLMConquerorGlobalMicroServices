using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;

namespace MLMConquerorGlobalEdition.SharedComponents.Server.Services;

/// <summary>
/// La cookie del idioma (<c>.AspNetCore.Culture</c>), con las mismas opciones en los tres sitios
/// que la escriben: el selector de idioma de cada portal y el inicio de sesión.
/// </summary>
/// <remarks>
/// <c>HttpOnly</c> y <c>Secure</c>: solo la lee el servidor (<c>CookieRequestCultureProvider</c>) y
/// el atributo <c>lang</c> de la página ya sale de ahí. <c>SameSite=Lax</c> y no <c>Strict</c>: el
/// idioma tiene que seguir cuando el usuario llega desde un enlace de otro sitio.
/// </remarks>
public static class CookieDeIdioma
{
    public static void Escribir(HttpResponse response, string cultura) =>
        response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(cultura, cultura)),
            Opciones());

    public static CookieOptions Opciones() => new()
    {
        // UtcNow y no Now: es una fecha de protocolo HTTP, no un dato de negocio.
        Expires     = DateTimeOffset.UtcNow.AddYears(1),
        SameSite    = SameSiteMode.Lax,
        Secure      = true,
        HttpOnly    = true,
        IsEssential = true,
        Path        = "/"
    };
}
