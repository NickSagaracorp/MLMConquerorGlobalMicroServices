using Microsoft.Extensions.DependencyInjection;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>One call registers the single placement authority. Every API/job that places
/// members into the dual tree must resolve <see cref="IPlacementService"/> so all writers
/// share one correct, idempotent, concurrency-safe implementation.</summary>
public static class PlacementServicesRegistration
{
    public static IServiceCollection AddPlacementServices(this IServiceCollection services)
    {
        services.AddScoped<IPlacementService, PlacementService>();
        return services;
    }
}
