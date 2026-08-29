using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MLMConquerorGlobalEdition.SharedComponents.Services;

namespace MLMConquerorGlobalEdition.SharedComponents.Extensions;

public static class SharedComponentsServiceExtensions
{
    /// <summary>
    /// Registers all services required by the SharedComponents RCL.
    /// Call this from both BizCenter and AdminApp Program.cs.
    /// </summary>
    /// <remarks>
    /// La semilla del contexto de vista se registra con <c>TryAdd</c> y en su versión vacía: es la
    /// que sirve a las dos MAUI, donde el contexto llega completo desde el JWT por
    /// <c>ViewContextService.SetContext</c>. Un portal web pide la suya con
    /// <c>AddHttpContextViewContextSeed()</c> (en SharedComponents.Server), que la pone con
    /// <c>Replace</c> y por eso funciona igual la llame antes o después de este método.
    /// </remarks>
    public static IServiceCollection AddSharedComponents(this IServiceCollection services)
    {
        services.TryAddScoped<IViewContextSeed, NullViewContextSeed>();

        // Una sola instancia por ámbito, servida por los dos registros. Antes eran dos
        // AddScoped independientes —uno para la interfaz y otro para el tipo concreto— y eso
        // fabrica DOS objetos distintos: quien llama a SetContext (los inicializadores, que
        // inyectan el tipo concreto) escribía en uno, y los componentes, que inyectan la
        // interfaz, leían el otro. El contexto no llegaba nunca a la pantalla.
        //
        // En web el fallo quedaba tapado porque la instancia de la interfaz se rellena sola
        // desde HttpContextViewContextSeed. En las dos MAUI la semilla es la vacía, así que
        // allí la impersonación de AdminApp simplemente no surtía efecto.
        services.AddScoped<ViewContextService>();
        services.AddScoped<IViewContextService>(sp => sp.GetRequiredService<ViewContextService>());
        services.AddScoped<IThemeService, ThemeService>();
        services.AddLocalization();
        return services;
    }
}
