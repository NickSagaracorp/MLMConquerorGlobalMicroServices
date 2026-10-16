using Microsoft.Extensions.DependencyInjection;

namespace MLMConquerorGlobalEdition.Repository.Services.Entrax;

public static class EntraxServiceCollectionExtensions
{
    /// <summary>
    /// Registra las opciones (singleton) y el cliente: el fake si <see cref="EntraxOptions.UseFake"/>
    /// (la guarda de arranque ya impidió que eso pase fuera de Development), o el real como
    /// HttpClient tipado con 30 s de timeout, el mismo tope que los clientes de payout.
    /// </summary>
    public static IServiceCollection AddEntrax(this IServiceCollection services, EntraxOptions options)
    {
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
