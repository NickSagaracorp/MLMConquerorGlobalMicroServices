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
    /// <remarks>
    /// SINGLETON a propósito: el constructor importa las dos llaves RSA y deriva con HKDF las claves
    /// de la huella y del antirreplay del TOTP, y como Scoped repetía ese trabajo en cada petición
    /// que tocaba el segundo factor. Después de construirse el servicio no guarda estado mutable:
    /// llaves y claves derivadas son de solo lectura y el reloj se consulta en cada llamada.
    ///
    /// La condición es que <c>IDateTimeProvider</c> sea también singleton en el anfitrión —lo es en
    /// todos los que hoy lo registran—. Si un anfitrión lo registrara Scoped, el contenedor lo
    /// rechazaría al validar ámbitos en lugar de capturar en silencio una instancia de un ámbito.
    /// </remarks>
    public static IServiceCollection AddAuthnChallengeTokens(this IServiceCollection services)
    {
        services.AddSingleton<IChallengeTokenService, ChallengeTokenService>();
        return services;
    }

    /// <summary>
    /// Registra el firmante del permiso de step-up. Solo en el anfitrión que verifica códigos: es
    /// el único que puede afirmar que el usuario acaba de demostrar el segundo factor.
    /// </summary>
    public static IServiceCollection AddAuthnStepUpGrants(this IServiceCollection services)
    {
        services.AddScoped<IStepUpGrantService, StepUpGrantService>();
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
