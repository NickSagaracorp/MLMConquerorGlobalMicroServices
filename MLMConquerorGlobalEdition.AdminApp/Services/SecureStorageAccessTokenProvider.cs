using MLMConquerorGlobalEdition.ClientCore;

namespace MLMConquerorGlobalEdition.AdminApp.Services;

/// <summary>
/// El lado MÓVIL de <see cref="IAccessTokenProvider"/>: el token de acceso vigente sale del
/// almacenamiento seguro del dispositivo.
/// </summary>
/// <remarks>
/// Es la pieza que le faltaba a <see cref="AuthApiGateway"/> para funcionar dentro de una MAUI. En
/// los portales web la implementación es <c>HttpContextAccessTokenProvider</c>, que lo lee del claim
/// de la cookie de sesión; aquí no hay HttpContext ninguno, y por eso el gateway pide el token por
/// esta abstracción en vez de leerlo él mismo. Es exactamente el motivo por el que ClientCore no
/// puede depender de ASP.NET Core.
///
/// Devuelve el token del ADMINISTRADOR, no el de impersonación. Los dos endpoints de la API de
/// autenticación que este cliente llama con sesión —gestión de la cuenta— son sobre la cuenta de
/// quien está sentado delante, y mandarles el token de un miembro suplantado cambiaría la cuenta
/// que se está tocando sin que nadie lo pidiera. El token efectivo, con impersonación incluida, lo
/// sigue poniendo <see cref="AdminAuthHandler"/> en las llamadas a AdminAPI.
/// </remarks>
public sealed class SecureStorageAccessTokenProvider : IAccessTokenProvider
{
    private readonly AdminJwtAuthStateProvider _authProvider;

    public SecureStorageAccessTokenProvider(AdminJwtAuthStateProvider authProvider)
        => _authProvider = authProvider;

    public async ValueTask<string?> GetAccessTokenAsync(CancellationToken ct = default)
        => await _authProvider.GetAdminTokenAsync();
}
