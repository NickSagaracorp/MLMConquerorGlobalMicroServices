using Microsoft.Extensions.DependencyInjection;
using MLMConquerorGlobalEdition.SharedComponents.Services;

namespace MLMConquerorGlobalEdition.SharedComponents.Extensions;

public static class SharedComponentsServiceExtensions
{
    /// <summary>
    /// Registers all services required by the SharedComponents RCL.
    /// Call this from both BizCenter and AdminApp Program.cs.
    /// </summary>
    public static IServiceCollection AddSharedComponents(this IServiceCollection services)
    {
        services.AddScoped<IViewContextService, ViewContextService>();
        services.AddScoped<ViewContextService>();   // also register concrete type for SetContext()
        services.AddScoped<IThemeService, ThemeService>();
        services.AddLocalization();
        return services;
    }
}
