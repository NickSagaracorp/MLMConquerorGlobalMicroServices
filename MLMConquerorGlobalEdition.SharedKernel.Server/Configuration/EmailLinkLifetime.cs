using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MLMConquerorGlobalEdition.SharedKernel.Server.Configuration;

/// <summary>
/// Cuánto vive un ENLACE DE CORREO —confirmación de dirección y recuperación de contraseña—, en un
/// solo sitio.
/// </summary>
/// <remarks>
/// EL PROBLEMA QUE RESUELVE. Esta cifra vive en DOS sitios a la vez y tiene que ser la misma en los
/// dos: en <c>DataProtectionTokenProviderOptions.TokenLifespan</c>, que es lo que de verdad hace
/// caducar el token de Identity, y en el TEXTO DEL CORREO, que es lo que el usuario lee. Hasta ahora
/// no había ningún sitio: la vigencia era el valor por defecto de Identity (un día, nunca escrito en
/// ninguna parte) y los manejadores llevaban un <c>const int ExpiresInHours = 24</c> copiado dos
/// veces cuyo comentario decía "las dos cifras tienen que coincidir" y confiaba en que alguien se
/// acordara. Un correo que anuncia una caducidad distinta de la real es peor que no anunciar nada:
/// miente al usuario justo en el momento en que está bloqueado fuera de su cuenta.
///
/// Ahora la cifra sale de aquí para los dos usos: <see cref="AddEmailLinkTokenLifetime"/> la mete en
/// las opciones del proveedor de Identity y <see cref="Minutes"/> es lo que los manejadores ponen en
/// la variable <c>{{ExpiresInMinutes}}</c> de la plantilla. Divergir deja de ser posible sin editar
/// este archivo.
///
/// POR QUÉ 15 MINUTOS. Un enlace de recuperación es una credencial de un solo uso que viaja por
/// correo y se queda en el buzón: mientras vive, cualquiera con acceso a ese buzón —o al portátil
/// desbloqueado que lo tiene abierto— es dueño de la cuenta. El día entero por defecto de Identity
/// era una ventana absurda para algo que se usa en el minuto siguiente a pedirlo.
///
/// QUÉ NO TOCA. <c>TokenLifespan</c> es de <c>DataProtectionTokenProvider</c>, que es el que emite
/// los tokens de confirmación de correo, de recuperación de contraseña y de cambio de dirección. NO
/// es el del autenticador: el segundo factor va por
/// <c>TokenOptions.DefaultAuthenticatorProvider</c>, que es TOTP con su propia ventana de tiempo, y
/// el reto de 2FA del portal va por <c>Auth:TwoFactor:ChallengeLifetimeMinutes</c>, que es otro
/// mecanismo distinto. Nada de eso se mueve desde aquí.
/// </remarks>
public static class EmailLinkLifetime
{
    /// <summary>Clave de configuración que permite ajustarlo sin recompilar.</summary>
    public const string ConfigKey = "Auth:EmailLinkLifetimeMinutes";

    /// <summary>
    /// Lo que rige si la clave no está. Es el valor de la política, no un relleno.
    /// </summary>
    /// <remarks>
    /// Solo SignupAPI declara la clave en su <c>appsettings.json</c>, porque es el único anfitrión
    /// que emite y valida estos enlaces. AdminAPI y BizCenter registran Identity y por tanto el
    /// mismo proveedor de tokens, pero no mandan ninguno de estos correos: caen en este valor por
    /// defecto, que es el mismo número, y así no hay una tercera y una cuarta copia de la cifra
    /// esperando a que alguien actualice solo una.
    /// </remarks>
    public const int DefaultMinutes = 15;

    /// <summary>Los minutos configurados. Es lo que se le dice al usuario en el correo.</summary>
    public static int Minutes(IConfiguration config) =>
        config.GetValue(ConfigKey, DefaultMinutes);

    /// <summary>La misma cifra como <see cref="TimeSpan"/>.</summary>
    public static TimeSpan Span(IConfiguration config) =>
        TimeSpan.FromMinutes(Minutes(config));

    /// <summary>
    /// Aplica la vigencia al proveedor de tokens de Identity. Va DESPUÉS de
    /// <c>AddDefaultTokenProviders()</c> en el <c>Program.cs</c> de cada anfitrión que registre
    /// Identity, aunque ese anfitrión no mande correos: dos anfitriones con la misma Identity y
    /// vigencias distintas es la clase de divergencia que no se nota hasta que un token emitido por
    /// uno se valida en el otro.
    /// </summary>
    public static IServiceCollection AddEmailLinkTokenLifetime(
        this IServiceCollection services, IConfiguration config)
    {
        var vigencia = Span(config);

        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = vigencia);

        return services;
    }
}
