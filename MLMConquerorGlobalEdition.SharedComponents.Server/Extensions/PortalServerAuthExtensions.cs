using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.SharedComponents.Server.Services;
using MLMConquerorGlobalEdition.SharedComponents.Services;

namespace MLMConquerorGlobalEdition.SharedComponents.Server.Extensions;

/// <summary>
/// El alta de las piezas de sesión que un portal web comparte con el otro.
/// </summary>
/// <remarks>
/// Las dos piezas de aquí llevan un dato del portal en el constructor —un booleano en una, una ruta
/// en la otra—, así que no se pueden registrar con el <c>AddScoped&lt;T&gt;</c> de siempre. El
/// registro con fábrica podría escribirse en cada <c>Program.cs</c>, pero entonces el cableado
/// vuelve a estar copiado en dos sitios, que es justo lo que se estaba deshaciendo: cada portal
/// dice QUÉ es distinto y esta clase decide CÓMO se monta.
/// </remarks>
public static class PortalServerAuthExtensions
{
    /// <summary>
    /// Registra el inicializador del contexto de vista del portal.
    /// </summary>
    /// <param name="isAdminContext">
    /// Si este portal mira siempre en contexto de administrador. El portal de administración, sí;
    /// el centro de negocios, no.
    /// </param>
    /// <remarks>
    /// Resuelve el tipo CONCRETO <c>ViewContextService</c>, que es el registro real que deja
    /// <c>AddSharedComponents</c> y del que cuelga <c>IViewContextService</c>. Así el inicializador
    /// y los componentes hablan con el mismo objeto.
    /// </remarks>
    public static IServiceCollection AddServerViewContextInitializer(
        this IServiceCollection services, bool isAdminContext)
    {
        // Dependencia dura del inicializador; TryAdd por dentro, no estorba a quien ya lo llame.
        services.AddHttpContextAccessor();

        services.AddScoped(sp => new ServerViewContextInitializer(
            sp.GetRequiredService<IHttpContextAccessor>(),
            sp.GetRequiredService<ViewContextService>(),
            isAdminContext));

        return services;
    }

    /// <summary>
    /// Registra el manejador que lleva el JWT del usuario a las APIs del portal, y con él la puerta
    /// que le deja alcanzar los servicios del circuito de quien llama.
    /// </summary>
    /// <param name="loginPage">
    /// La pantalla de login de este portal, a la que se manda al usuario cuando su sesión caduca.
    /// </param>
    /// <param name="logoutRoute">
    /// La salida de este portal. Es a donde manda el manejador desde dentro del circuito: pasar por
    /// ahí es lo único que puede limpiar la cookie de sesión, y esa salida redirige al login con el
    /// aviso. Las dos aplicaciones la montan en la misma ruta, de ahí el valor por defecto.
    /// </param>
    /// <remarks>
    /// El manejador es transitorio, como pide <c>AddHttpMessageHandler</c>: la fábrica de clientes
    /// HTTP construye una cadena de manejadores por cliente y la reutiliza. Registrar el manejador
    /// NO lo engancha a nada; eso lo hace cada
    /// <c>AddHttpClient(...).AddHttpMessageHandler&lt;ApiAuthHandler&gt;()</c>, y por eso el mismo
    /// manejador sirve a los tres clientes de administración y al del centro de negocios.
    ///
    /// AQUÍ NO SE INYECTA NINGÚN SERVICIO CON ÁMBITO, y no es un descuido: la cadena se construye en
    /// el ámbito de la fábrica, así que un <c>NavigationManager</c> o un
    /// <c>AuthenticationStateProvider</c> pedidos desde este <c>sp</c> no serían nunca los del
    /// circuito del usuario. Eso es exactamente lo que hacía que una sesión caducada no llevara a
    /// ninguna parte. Los del circuito llegan en tiempo de ejecución por
    /// <see cref="CircuitServicesAccessor"/>.
    /// </remarks>
    public static IServiceCollection AddPortalApiAuthHandler(
        this IServiceCollection services, string loginPage, string logoutRoute = "/account/logout")
    {
        services.AddHttpContextAccessor();

        // Singleton: el valor vive en un AsyncLocal y todos —el circuito que lo rellena y el
        // manejador que lo lee desde otro ámbito— tienen que estar mirando el mismo.
        services.TryAddSingleton<CircuitServicesAccessor>();

        // Con ámbito y por enumerable, que es como Blazor resuelve los CircuitHandler: se construye
        // uno por circuito y recibe el proveedor de servicios de ESE circuito, que es justo lo que
        // hay que publicar.
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<CircuitHandler, CircuitServicesAccessorHandler>());

        services.AddTransient(sp => new ApiAuthHandler(
            sp.GetRequiredService<IHttpContextAccessor>(),
            sp.GetRequiredService<CircuitServicesAccessor>(),
            sp.GetRequiredService<ILogger<ApiAuthHandler>>(),
            loginPage,
            logoutRoute));

        return services;
    }

    /// <summary>
    /// Corta las navegaciones de un usuario con la sesión caducada y lo manda al login con el aviso.
    /// </summary>
    /// <remarks>
    /// Va DESPUÉS de <c>UseAuthentication()</c> —necesita el <c>ClaimsPrincipal</c> de la cookie para
    /// poder mirar el token que lleva dentro— y antes de que nada empiece a escribir la respuesta.
    ///
    /// Es la mitad del arreglo que ocurre fuera del circuito; la otra mitad, la de dentro, la hace
    /// <see cref="ApiAuthHandler"/>. Hacen falta las dos: el primer render de un circuito recién
    /// abierto no es actividad entrante y aquel camino no llega a dispararse, así que una recarga con
    /// el token ya caducado se comería el 401 en la pantalla si esto no estuviera.
    /// </remarks>
    public static IApplicationBuilder UseSessionExpiry(this IApplicationBuilder app) =>
        app.UseMiddleware<SessionExpiryMiddleware>();
}
