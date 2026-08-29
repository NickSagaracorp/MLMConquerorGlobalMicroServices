using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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
    /// Registra el manejador que lleva el JWT del usuario a las APIs del portal.
    /// </summary>
    /// <param name="loginPage">
    /// La pantalla de login de este portal, a la que se manda al usuario cuando su sesión caduca.
    /// </param>
    /// <remarks>
    /// Transitorio, como pide <c>AddHttpMessageHandler</c>: la fábrica de clientes HTTP construye
    /// una cadena de manejadores por cliente y la reutiliza. Registrar el manejador NO lo engancha
    /// a nada; eso lo hace cada <c>AddHttpClient(...).AddHttpMessageHandler&lt;ApiAuthHandler&gt;()</c>,
    /// y por eso el mismo manejador sirve a los tres clientes de administración.
    /// </remarks>
    public static IServiceCollection AddPortalApiAuthHandler(
        this IServiceCollection services, string loginPage)
    {
        services.AddHttpContextAccessor();

        services.AddTransient(sp => new ApiAuthHandler(
            sp.GetRequiredService<IHttpContextAccessor>(),
            sp.GetRequiredService<AuthenticationStateProvider>(),
            sp.GetRequiredService<NavigationManager>(),
            loginPage));

        return services;
    }
}
