using Microsoft.Extensions.DependencyInjection;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Authn.Services;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Authn;

public static class AuthnServiceCollectionExtensions
{
    /// <summary>
    /// Registra el emisor de tokens de acceso. Depende de <c>IConfiguration</c>, que cada host ya
    /// registra por su cuenta.
    /// </summary>
    /// <remarks>
    /// Lo llaman los DOS anfitriones que firman tokens: SignupAPI, que es la puerta única de
    /// entrada, y AdminAPI, que solo lo necesita para la impersonación. Cualquier otro host que
    /// añada esta línea está abriendo una segunda puerta y hay que mirarlo con lupa.
    /// </remarks>
    public static IServiceCollection AddAuthnAccessTokens(this IServiceCollection services)
    {
        services.AddScoped<IJwtService, JwtService>();
        return services;
    }

    /// <summary>
    /// Registra el challenge firmado. El servicio depende de <c>IConfiguration</c> y de
    /// <c>IDateTimeProvider</c>, que cada host ya registra por su cuenta.
    /// </summary>
    public static IServiceCollection AddAuthnChallengeTokens(this IServiceCollection services)
    {
        services.AddScoped<IChallengeTokenService, ChallengeTokenService>();
        return services;
    }

    /// <summary>
    /// Registra el enrolamiento TOTP. Depende de <c>UserManager&lt;ApplicationUser&gt;</c>,
    /// <c>IConfiguration</c> y <c>IDateTimeProvider</c>, que cada host ya registra por su cuenta.
    /// </summary>
    public static IServiceCollection AddAuthnTotpEnrollment(this IServiceCollection services)
    {
        services.AddScoped<ITotpEnrollmentService, TotpEnrollmentService>();
        return services;
    }

    /// <summary>
    /// Registra la orquestación de los tres canales. Depende de <c>IChallengeTokenService</c>
    /// —registrado por <see cref="AddAuthnChallengeTokens"/>— y de
    /// <c>UserManager&lt;ApplicationUser&gt;</c>, <c>IEmailService</c>, <c>ISmsService</c>,
    /// <c>IEncryptionService</c>, <c>ICacheService</c>, <c>IDateTimeProvider</c> e
    /// <c>IConfiguration</c>, que cada host registra por su cuenta.
    /// </summary>
    public static IServiceCollection AddAuthnTwoFactor(this IServiceCollection services)
    {
        services.AddScoped<ITwoFactorService, TwoFactorService>();
        return services;
    }
}
