using Microsoft.AspNetCore.Http;
using MLMConquerorGlobalEdition.ClientCore;

namespace MLMConquerorGlobalEdition.SharedComponents.Server.Services;

/// <summary>
/// El lado web de <see cref="IAccessTokenProvider"/>: el token de acceso VIGENTE del usuario de esta
/// petición, renovándolo contra la API si el de su cookie ya caducó.
///
/// Se queda aquí, y no en ClientCore, porque <c>HttpContext</c> es exactamente la dependencia de
/// alojamiento web de la que había que librar al gateway. Aquí no estorba: SharedComponents ya es
/// una biblioteca Razor con referencia al framework de ASP.NET Core, y sus dos consumidores son
/// portales.
///
/// De ámbito de petición, como todo lo que lee del <c>HttpContext</c>: el token que devuelve es
/// el del usuario de ESTA petición y de ninguna otra.
/// </summary>
/// <remarks>
/// POR QUÉ RENUEVA AQUÍ Y NO SOLO EN EL MIDDLEWARE. Por este proveedor pasa TODO lo que el portal le
/// pide a SignupAPI con sesión: cambiar la contraseña, dar de alta un teléfono, apagar el segundo
/// factor, descargar los datos personales, salir. Ninguna de esas cosas es una navegación —son POST
/// de formulario—, así que el middleware no las mira, y con el JWT caducado todas devolvían
/// <c>SESSION_EXPIRED</c> aunque el usuario acabara de teclear su contraseña actual. Renovando en el
/// único sitio por el que pasan todas, funcionan las cinco sin tocar ninguna.
///
/// La cookie no se reemite desde aquí: hay peticiones en las que la respuesta ya empezó, y decidir
/// caso por caso sería repartir por el archivo una regla que ya está en un sitio. Los tokens nuevos
/// quedan en <see cref="PortalSessionTokens"/> y la cookie se pone al día en la siguiente
/// navegación, que es el mismo trato que tiene la renovación dentro de un circuito.
/// </remarks>
public sealed class HttpContextAccessTokenProvider : IAccessTokenProvider
{
    private readonly IHttpContextAccessor  _httpContextAccessor;
    private readonly PortalSessionTokens   _sessionTokens;

    public HttpContextAccessTokenProvider(
        IHttpContextAccessor httpContextAccessor, PortalSessionTokens sessionTokens)
    {
        _httpContextAccessor = httpContextAccessor;
        _sessionTokens       = sessionTokens;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Devuelve null cuando no hay <c>HttpContext</c> (por ejemplo, fuera de una petición), cuando
    /// el usuario no tiene sesión y cuando la sesión ya no se puede renovar. Las tres son, para
    /// quien llama, la misma: no hay token, y el gateway lo traduce a <c>SESSION_EXPIRED</c>.
    ///
    /// Solo cede el hilo cuando hay que renovar de verdad. Con el token todavía vivo —que es el caso
    /// de casi todas las llamadas— la comprobación es la lectura de un claim que ya está en memoria.
    /// </remarks>
    public async ValueTask<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        var vigentes = await _sessionTokens.EnsureFreshAsync(user, ct);
        return vigentes?.AccessToken;
    }
}
