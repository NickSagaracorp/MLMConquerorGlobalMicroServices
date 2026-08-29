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
        services.AddScoped<IViewContextService, ViewContextService>();
        services.AddScoped<ViewContextService>();   // also register concrete type for SetContext()
        services.AddScoped<IThemeService, ThemeService>();
        services.AddLocalization();
        return services;
    }
}
