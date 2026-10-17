using MLMConquerorGlobalEdition.Billing.Jobs;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;

namespace MLMConquerorGlobalEdition.Billing.Extensions;

public static class AnnualFeeServiceCollectionExtensions
{
    /// <summary>
    /// Registra la cuota anual (Plan K). Va en un método para que la prueba de DI ejercite este mismo
    /// registro. Supone el reloj de Billing que registra Program.cs. El Bloque 3 añade aquí el cobro.
    /// </summary>
    public static IServiceCollection AddAnnualFeeServices(this IServiceCollection services)
    {
        services.AddScoped<IAnnualFeeStateEnrollment, AnnualFeeStateEnrollment>();
        services.AddScoped<IAnnualFeeTokenConsumer, AnnualFeeTokenConsumer>();
        services.AddScoped<IAnnualFeeChargeService, AnnualFeeChargeService>();
        services.AddScoped<AnnualFeeStateEnrollmentJob>();
        return services;
    }
}
