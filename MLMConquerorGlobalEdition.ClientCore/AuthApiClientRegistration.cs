using Microsoft.Extensions.DependencyInjection;

namespace MLMConquerorGlobalEdition.ClientCore;

/// <summary>
/// El alta del cliente HTTP con el que TODO cliente —los dos portales web y las dos MAUI— habla
/// con SignupAPI.
/// </summary>
public static class AuthApiClientRegistration
{
    /// <summary>
    /// Registra el cliente con nombre <see cref="AuthApiGateway.HttpClientName"/> contra
    /// <paramref name="baseAddress"/>.
    /// </summary>
    /// <param name="baseAddress">La dirección de SignupAPI, que sale de la configuración del anfitrión.</param>
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
    /// EN UNA MAUI IMPORTA POR OTRA RAZÓN, y por eso este registro bajó de SharedComponents.Server
    /// —que una MAUI no puede referenciar— hasta aquí. Con las cookies encendidas, el manejador se
    /// TRAGA el <c>Set-Cookie</c> del refresh token para metérselo en su contenedor, y
    /// <see cref="RefreshCookie.ReadFrom"/> no encuentra la cabecera: la aplicación se queda con el
    /// token de acceso y sin nada con que renovarlo, así que la sesión muere a los quince minutos.
    /// Que la misma decisión valga para los cuatro anfitriones es justo lo que evita que uno de
    /// ellos la copie mal.
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
