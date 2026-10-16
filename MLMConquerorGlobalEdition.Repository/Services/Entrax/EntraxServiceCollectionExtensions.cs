using Microsoft.Extensions.DependencyInjection;

namespace MLMConquerorGlobalEdition.Repository.Services.Entrax;

public static class EntraxServiceCollectionExtensions
{
    /// <summary>
    /// Registra las opciones (singleton) y el cliente: el fake si <see cref="EntraxOptions.UseFake"/>,
    /// o el real como HttpClient tipado con 30 s de timeout, el mismo tope que los clientes de payout.
    /// Pasa la guarda de arranque antes de registrar nada: aunque quien llame se la salte, el fake
    /// (o Entrax encendido sin credenciales) fuera de Development no llega a registrarse.
    /// </summary>
    public static IServiceCollection AddEntrax(this IServiceCollection services, EntraxOptions options, bool isDevelopment)
    {
        EntraxOptionsGuard.ThrowIfInvalid(options, isDevelopment);

        services.AddSingleton(options);

        if (options.UseFake)
        {
            services.AddSingleton<FakeEntraxClient>();
            services.AddSingleton<IEntraxClient>(sp => sp.GetRequiredService<FakeEntraxClient>());
            return services;
        }

        services.AddHttpClient<IEntraxClient, EntraxClient>(c => c.Timeout = TimeSpan.FromSeconds(30));
        return services;
    }
}
