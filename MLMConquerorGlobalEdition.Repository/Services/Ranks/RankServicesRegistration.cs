using Microsoft.Extensions.DependencyInjection;

namespace MLMConquerorGlobalEdition.Repository.Services.Ranks;

/// <summary>One call registers every rank service. Every API that evaluates or displays
/// ranks must call this so all services agree on one implementation.</summary>
public static class RankServicesRegistration
{
    public static IServiceCollection AddRankServices(this IServiceCollection services)
    {
        services.AddScoped<IEnrollmentTeamPointsService, EnrollmentTeamPointsService>();
        services.AddScoped<IPersonalCustomerPointsService, PersonalCustomerPointsService>();
        services.AddScoped<IRankQualificationService, RankQualificationService>();
        services.AddScoped<IRankComputationService, RankComputationService>();
        return services;
    }
}
