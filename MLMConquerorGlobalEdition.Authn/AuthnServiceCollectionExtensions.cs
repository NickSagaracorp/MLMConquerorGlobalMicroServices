using Microsoft.Extensions.DependencyInjection;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Authn.Services;

namespace MLMConquerorGlobalEdition.Authn;

public static class AuthnServiceCollectionExtensions
{
    /// <summary>
    /// Registra el challenge firmado. El servicio depende de <c>IConfiguration</c> y de
    /// <c>IDateTimeProvider</c>, que cada host ya registra por su cuenta.
    /// </summary>
    public static IServiceCollection AddAuthnChallengeTokens(this IServiceCollection services)
    {
        services.AddScoped<IChallengeTokenService, ChallengeTokenService>();
        return services;
    }
}
