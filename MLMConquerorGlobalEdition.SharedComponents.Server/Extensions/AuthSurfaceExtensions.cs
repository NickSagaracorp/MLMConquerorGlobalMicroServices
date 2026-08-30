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

        // Los tokens vigentes de cada sesión y quien los renueva. La puerta es quien SIEMBRA el
        // almacén al firmar, así que sin esto el refresh token que acaba de capturar no tendría
        // dónde quedarse.
        services.AddPortalSessionTokens();

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

        // Lo que las DOS PANTALLAS DE LA PUERTA piden durante el render y no cabe en un formulario:
        // con qué canal se emitió el reto (la de verificación) y el QR con la clave compartida (la
        // del enrolamiento). Estaba solo en AddAccountSurface, y ahí no llegaba: un portal que monta
        // la puerta sin el área de cuenta —el centro de negocios— se quedaba sin poder inyectarlo en
        // sus propias pantallas de segundo factor y enrolamiento, que son suyas y de la puerta.
        // Scoped: lee las cookies HttpOnly del reto del HttpContext de la petición.
        services.TryAddScoped<TwoFactorPageData>();

        return services;
    }

    /// <summary>
    /// El cliente HTTP con el que los dos portales hablan con SignupAPI.
    /// </summary>
    /// <param name="baseAddress">La dirección de SignupAPI, que sale de la configuración del portal.</param>
    /// <remarks>
    /// ESTABA EN LOS DOS <c>Program.cs</c> COMO UN <c>AddHttpClient</c> A SECAS, y así ya no puede
    /// estar: este cliente lleva ahora refresh tokens, y con las cookies del manejador encendidas
    /// —que es como vienen por defecto— eso es una fuga entre usuarios.
    ///
    /// EL PROBLEMA, en concreto. <c>IHttpClientFactory</c> construye UN manejador primario por
    /// cliente con nombre y lo reutiliza para TODAS las llamadas de TODOS los usuarios.
    /// <c>HttpClientHandler.UseCookies</c> viene en <c>true</c>, así que ese manejador tiene un
    /// <c>CookieContainer</c> COMPARTIDO: el <c>Set-Cookie</c> con el que SignupAPI entrega el
    /// refresh token de quien acaba de entrar se guardaría ahí, y saldría enganchado en la siguiente
    /// llamada de cualquier otro usuario. Además se sumaría a la cabecera <c>Cookie</c> que la
    /// renovación pone a mano, mandando dos refresh tokens distintos en la misma petición.
    ///
    /// Con <c>UseCookies = false</c> el manejador no guarda ni reenvía nada, y cada llamada lleva
    /// exactamente el token que quien la hace le puso. En un navegador esto no se decide; aquí sí,
    /// y hay que decidirlo bien.
    ///
    /// Se registra por aquí y no en cada <c>Program.cs</c> por lo mismo que el resto de esta
    /// superficie: es una decisión de seguridad que no se ve desde la línea que la copia.
    /// </remarks>
    public static IHttpClientBuilder AddAuthApiClient(
        this IServiceCollection services, string baseAddress) =>
        services
            .AddHttpClient(AuthApiGateway.HttpClientName, client =>
            {
                client.BaseAddress = new Uri(baseAddress);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false });
}
