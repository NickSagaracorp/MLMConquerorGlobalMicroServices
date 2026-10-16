using Microsoft.Extensions.DependencyInjection;

namespace MLMConquerorGlobalEdition.Repository.Services.Links;

public static class MyLinksServiceCollectionExtensions
{
    /// <summary>
    /// Pasa la guarda ANTES de registrar nada (como AddEntrax): con una plantilla inválida el
    /// anfitrión no arranca, en cualquier entorno.
    /// </summary>
    public static IServiceCollection AddMyLinks(this IServiceCollection services, MyLinksOptions options)
    {
        MyLinksOptionsGuard.ThrowIfInvalid(options);

        services.AddSingleton(options);
        services.AddScoped<IMyLinksService, MyLinksService>();
        return services;
    }
}
