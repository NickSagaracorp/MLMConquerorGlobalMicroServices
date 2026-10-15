using Microsoft.Extensions.DependencyInjection;
using MLMConquerorGlobalEdition.SharedComponents.Components.Account.StepUp;
using MLMConquerorGlobalEdition.SharedComponents.Server.Services;

namespace MLMConquerorGlobalEdition.SharedComponents.Server.Extensions;

public static class StepUpClientRegistration
{
    /// <summary>
    /// Cliente a SignupAPI para el step-up, autenticado con <see cref="ApiAuthHandler"/> —el que
    /// funciona dentro del circuito de Blazor— y con las cookies del manejador apagadas por la
    /// misma razón que <c>AddAuthApiClient</c>: el manejador primario es compartido entre usuarios.
    /// </summary>
    public static IServiceCollection AddStepUpClient(this IServiceCollection services, string authApiBaseAddress)
    {
        services.AddHttpClient(StepUpClient.HttpClientName, c => c.BaseAddress = new Uri(authApiBaseAddress))
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false })
                .AddHttpMessageHandler<ApiAuthHandler>();
        services.AddScoped<IStepUpClient, StepUpClient>();
        return services;
    }
}
