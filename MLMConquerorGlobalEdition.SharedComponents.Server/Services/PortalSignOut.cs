using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using MLMConquerorGlobalEdition.ClientCore;

namespace MLMConquerorGlobalEdition.SharedComponents.Server.Services;

/// <summary>
/// QUÉ SIGNIFICA MATAR UNA SESIÓN DEL PORTAL, en un solo sitio y en un solo orden.
///
/// Lo ejecuta la salida de la puerta (<see cref="AuthEndpoints.LogoutAsync"/>), y por ahí pasan los
/// TRES caminos que matan una sesión de verdad: el usuario que pulsa "Salir", el circuito que
/// descubre su sesión caducada, y el REBOTE de la aplicación de alta —que cae en la misma salida con
/// un <c>returnUrl</c>— cuando alguien carga el asistente de alta en un navegador donde la persona
/// anterior se dejó la sesión abierta.
/// </summary>
/// <remarks>
/// POR QUÉ ESTO ES UNA CLASE Y NO CUATRO LÍNEAS COPIADAS EN CADA SITIO: una sesión del portal está
/// en CUATRO sitios a la vez y no en uno, y saltarse cualquiera de los cuatro deja algo vivo que
/// parece muerto.
///
///   • El refresh token, EN LA BASE DE DATOS DE LA API. Es la pieza que no está en el navegador y
///     por eso es la que se olvida: dura treinta días y sirve para pedir tokens de acceso nuevos
///     sin contraseña. Borrar la cookie sin invalidarlo no es cerrar la sesión, es esconderla.
///   • La entrada de <see cref="PortalSessionTokens"/>, en la memoria del portal. Dejarla viva
///     mantiene a mano una pareja de tokens con la que una petición en vuelo puede resucitar la
///     sesión que se acaba de cerrar.
///   • Las tres cookies de reto. Un segundo factor o un alta de teléfono a medias del usuario
///     anterior no puede sobrevivirle: son credenciales de un solo paso que otra persona podría
///     canjear.
///   • La cookie de sesión y —esto se olvida siempre— el <c>ClaimsPrincipal</c> de ESTA petición.
///     <c>SignOutAsync</c> escribe una cabecera para el navegador; no toca lo que el resto de la
///     tubería ya tiene en la mano. Sin la última línea, el middleware siguiente, la autorización y
///     el apretón de manos del circuito seguirían viendo a un usuario que ya no existe.
///
/// EL ORDEN NO ES DECORATIVO. La llamada a la API va PRIMERO porque va autenticada: necesita el
/// token del usuario, y ese token sale del <c>ClaimsPrincipal</c> que las dos últimas líneas se
/// llevan por delante. Invertirlo deja el refresh token vivo para siempre y sin un solo error a la
/// vista.
/// </remarks>
public static class PortalSignOut
{
    /// <summary>El endpoint de SignupAPI que invalida el refresh token de la cuenta.</summary>
    private const string LogoutPath = "api/v1/auth/logout";

    /// <summary>
    /// Mata la sesión de este navegador entera, en el orden de arriba.
    /// </summary>
    /// <returns>
    /// Si había una sesión que matar. Hoy no lo mira nadie —la salida va al mismo sitio en los dos
    /// casos—, pero se devuelve porque es el único dato con el que se puede distinguir un rebote que
    /// hizo su trabajo de uno que llegó sin cookie, que es exactamente lo que pasaría si algún día
    /// la aplicación de alta se sirviera desde otro dominio registrable.
    /// </returns>
    /// <remarks>
    /// EL RESULTADO DE LA API SE IGNORA A PROPÓSITO. Pase lo que pase al otro lado —servicio caído,
    /// token ya caducado, red que no responde—, la sesión del portal se cierra igual. Una salida
    /// que se quedara a medias porque SignupAPI no contesta sería peor que una que deja un refresh
    /// token vivo hasta que caduque solo.
    ///
    /// Si el token de acceso del usuario ya caducó, el proveedor de token RENUEVA antes de esta
    /// llamada: es lo único que permite invalidar el refresco de una sesión a la que se llega tarde.
    /// Y si ni siquiera se puede renovar, es que el refresco ya estaba muerto y no queda nada que
    /// invalidar.
    ///
    /// Las cookies de reto se borran haya o no haya sesión: un reto a medias vive en su propia
    /// cookie y sobrevive perfectamente a un usuario anónimo.
    /// </remarks>
    public static async Task<bool> KillAsync(
        HttpContext          httpContext,
        AuthApiGateway       api,
        ChallengeCookieNames challengeCookies,
        PortalSessionTokens  sessionTokens,
        CancellationToken    ct = default)
    {
        var habiaSesion = httpContext.User.Identity?.IsAuthenticated == true;

        if (habiaSesion)
        {
            // 1. El refresh token en la API, mientras el principal de esta petición todavía existe.
            await api.CallAsync(HttpMethod.Post, LogoutPath, null, authenticated: true, ct);

            // 2. La entrada del almacén de sesión del portal.
            sessionTokens.Forget(httpContext.User);
        }

        // 3. Los retos a medias: segundo factor, enrolamiento y alta de teléfono.
        ChallengeCookies.Delete(httpContext, challengeCookies.Login);
        ChallengeCookies.Delete(httpContext, challengeCookies.Enrollment);
        ChallengeCookies.Delete(httpContext, challengeCookies.Phone);

        // 4. La cookie de sesión del portal.
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // 5. Y el usuario de esta petición, que es lo que ve todo lo que venga después.
        httpContext.User = Anonimo;

        return habiaSesion;
    }

    /// <summary>Un principal sin identidad autenticada: nadie.</summary>
    private static ClaimsPrincipal Anonimo => new(new ClaimsIdentity());
}
