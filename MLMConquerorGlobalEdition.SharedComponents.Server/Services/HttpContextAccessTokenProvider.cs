using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MLMConquerorGlobalEdition.ClientCore;

namespace MLMConquerorGlobalEdition.SharedComponents.Server.Services;

/// <summary>
/// El lado web de <see cref="IAccessTokenProvider"/>: saca el token del claim
/// <c>access_token</c> de la cookie de sesión, que es donde lo dejó
/// <c>AuthEndpoints.CompleteSignInAsync</c>.
///
/// Se queda aquí, y no en ClientCore, porque <c>HttpContext</c> es exactamente la dependencia de
/// alojamiento web de la que había que librar al gateway. Aquí no estorba: SharedComponents ya es
/// una biblioteca Razor con referencia al framework de ASP.NET Core, y sus dos consumidores son
/// portales.
///
/// De ámbito de petición, como todo lo que lee del <c>HttpContext</c>: el token que devuelve es
/// el del usuario de ESTA petición y de ninguna otra.
/// </summary>
public sealed class HttpContextAccessTokenProvider : IAccessTokenProvider
{
    /// <summary>Nombre del claim donde el manejador de login guardó el token.</summary>
    private const string AccessTokenClaim = "access_token";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextAccessTokenProvider(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    /// <inheritdoc />
    /// <remarks>
    /// Sin ceder el hilo: el claim ya está en memoria —lo trajo la autenticación por cookie al
    /// principio de la petición—, así que no hay nada que esperar. La firma es asíncrona por lo
    /// que necesita móvil, no por lo que necesita web.
    ///
    /// Devuelve null cuando no hay <c>HttpContext</c> (por ejemplo, fuera de una petición) o
    /// cuando el usuario no tiene sesión. Las dos cosas son, para quien llama, la misma: no hay
    /// token.
    /// </remarks>
    public ValueTask<string?> GetAccessTokenAsync(CancellationToken ct = default) =>
        ValueTask.FromResult(
            _httpContextAccessor.HttpContext?.User.FindFirstValue(AccessTokenClaim));
}
