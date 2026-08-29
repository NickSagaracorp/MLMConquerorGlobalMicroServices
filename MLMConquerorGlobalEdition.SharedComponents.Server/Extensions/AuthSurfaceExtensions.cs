using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MLMConquerorGlobalEdition.ClientCore;
using MLMConquerorGlobalEdition.SharedComponents.Server.Services;

namespace MLMConquerorGlobalEdition.SharedComponents.Server.Extensions;

/// <summary>
/// El alta de la PUERTA del portal: lo que necesitan los manejadores de
/// <see cref="AuthEndpoints"/> para funcionar.
///
/// Aparte de <see cref="AccountSurfaceExtensions.AddAccountSurface"/> porque son dos superficies
/// distintas y un portal puede montar esta sin aquella — es exactamente lo que hace hoy el centro
/// de negocios, que ya tiene login pero todavía no el área de cuenta compartida.
/// </summary>
public static class AuthSurfaceExtensions
{
    /// <summary>
    /// Registra los destinos y la política de entrada de este portal, los nombres de sus cookies de
    /// reto y la única puerta a SignupAPI.
    /// </summary>
    /// <remarks>
    /// Lo que NO registra, porque es de cada portal: el cliente HTTP <c>"AuthApi"</c> —su dirección
    /// base sale de la configuración— y la autenticación por cookie.
    ///
    /// Todo va con <c>TryAdd</c> menos las opciones: un portal que monte además el área de cuenta
    /// pide estas mismas piezas por el otro lado, y llamar a las dos cosas no puede acabar en dos
    /// juegos de servicios.
    /// </remarks>
    public static IServiceCollection AddAuthSurface(
        this IServiceCollection services,
        AuthPortalOptions       portal,
        ChallengeCookieNames    challengeCookies)
    {
        // Destinos, roles admitidos e idioma. Inmutables y de toda la aplicación.
        services.AddSingleton(portal);

        // Los nombres de las cookies de reto. El mismo juego que lee la superficie de cuenta: es lo
        // que impide escribir un reto con un nombre y buscarlo con otro.
        services.AddChallengeCookieNames(challengeCookies);

        // Dependencia dura del proveedor de token. TryAdd por dentro.
        services.AddHttpContextAccessor();

        // De dónde saca el gateway el token del usuario. En un portal, del claim access_token de la
        // cookie de sesión. Aquí casi no se usa —el login es anónimo por definición—, pero el
        // gateway ni se construye sin él. Scoped porque lee del HttpContext de la petición.
        services.TryAddScoped<IAccessTokenProvider, HttpContextAccessTokenProvider>();

        // La única puerta a SignupAPI desde el portal: monta la llamada, desenvuelve el ApiResponse
        // y —lo que aquí importa— convierte un servicio caído en un código de error en vez de en un
        // 500 en la cara del usuario. Scoped porque su proveedor de token lo es.
        services.TryAddScoped<AuthApiGateway>();

        return services;
    }
}
