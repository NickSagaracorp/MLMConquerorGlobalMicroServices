using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MLMConquerorGlobalEdition.SharedComponents.Services;

namespace MLMConquerorGlobalEdition.SharedComponents.Extensions;

/// <summary>
/// El alta completa del área de cuenta en un portal: los servicios por un lado y las rutas por
/// otro.
///
/// Existe para que montar esta superficie en el segundo portal sea llamar a dos métodos y no
/// copiar veinte líneas de <c>Program.cs</c>. Las rutas son el sitio donde más caro sale copiar:
/// cuál lleva <c>RequireAuthorization()</c> y cuál no es una decisión de seguridad que no se ve
/// desde la pantalla, y una copia a la que se le olvide uno de esos sufijos deja abierto un
/// endpoint de gestión sin que nada falle a la vista.
/// </summary>
public static class AccountSurfaceExtensions
{
    /// <summary>
    /// Registra el cableado del área de cuenta con las rutas de pantalla de este portal.
    /// </summary>
    /// <remarks>
    /// Lo que NO registra, porque es de cada portal y no de esta biblioteca: el cliente HTTP
    /// "AuthApi" —su dirección base sale de la configuración del portal— y la autenticación por
    /// cookie. Los tres servicios de aquí son de ámbito de petición porque los tres dependen del
    /// <c>HttpContext</c>: de ahí salen el token de la sesión y las cookies de los retos.
    /// </remarks>
    public static IServiceCollection AddAccountSurface(
        this IServiceCollection services, AccountPageRoutes routes)
    {
        // Las rutas de pantalla son inmutables y valen para toda la aplicación.
        services.AddSingleton(routes);

        // Dependencia dura del gateway y de las dos clases de datos de página. Es TryAdd por
        // dentro, así que no estorba al portal que ya lo llame por su cuenta.
        services.AddHttpContextAccessor();

        // La única puerta a SignupAPI desde el portal: monta la llamada, le pone el Bearer del
        // claim access_token cuando hace falta, desenvuelve el ApiResponse y traduce el fallo a un
        // código. Scoped porque el token sale del HttpContext de la petición.
        services.AddScoped<AuthApiGateway>();

        // Datos que las páginas del área de cuenta piden durante el render. Scoped y con el
        // resultado memorizado: account-status se pide UNA vez por página aunque la pinten tres
        // componentes.
        services.AddScoped<AccountPageData>();

        // Datos que las páginas del segundo factor piden durante el render (canal del reto, QR y
        // clave del enrolamiento). Scoped: depende del HttpContext de la petición, que es de donde
        // salen las cookies HttpOnly del reto.
        services.AddScoped<TwoFactorPageData>();

        return services;
    }

    /// <summary>
    /// Monta los POST de los formularios del área de cuenta y el GET de la descarga de datos
    /// personales.
    /// </summary>
    /// <remarks>
    /// Las rutas de ENTRAR —login, logout, segundo factor y enrolamiento— no están aquí: las
    /// sirven los manejadores de <c>AuthEndpoints</c>, que siguen siendo de cada portal porque
    /// deciden quién puede entrar (administración exige rol de administrador) y a dónde va el
    /// usuario después de firmar la sesión.
    /// </remarks>
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Antiforgery desactivado en todos: son formularios HTML posteados desde páginas en SSR
        // estático, sin circuito interactivo que pueda llevar el token.
        //
        // Anónimos — el usuario todavía no tiene sesión.
        endpoints.MapPost("/account/forgot-password",     (Delegate)AccountEndpoints.ForgotPasswordAsync).DisableAntiforgery();
        endpoints.MapPost("/account/reset-password",      (Delegate)AccountEndpoints.ResetPasswordAsync).DisableAntiforgery();

        // De gestión — el Bearer sale del claim access_token de la cookie, que AuthApiGateway lee
        // del HttpContext. RequireAuthorization() los cierra antes de que el manejador llegue a
        // correr: sin él, una llamada sin sesión acabaría en el manejador y volvería con
        // SESSION_EXPIRED en la URL en vez de mandar al login, que es lo que el usuario necesita.
        endpoints.MapPost("/account/resend-confirmation", (Delegate)AccountEndpoints.ResendConfirmationAsync).DisableAntiforgery().RequireAuthorization();
        endpoints.MapPost("/account/change-password",     (Delegate)AccountEndpoints.ChangePasswordAsync).DisableAntiforgery().RequireAuthorization();
        endpoints.MapPost("/account/set-password",        (Delegate)AccountEndpoints.SetPasswordAsync).DisableAntiforgery().RequireAuthorization();
        endpoints.MapPost("/account/phone/add",           (Delegate)AccountEndpoints.AddPhoneAsync).DisableAntiforgery().RequireAuthorization();
        endpoints.MapPost("/account/phone/verify",        (Delegate)AccountEndpoints.VerifyPhoneAsync).DisableAntiforgery().RequireAuthorization();
        endpoints.MapPost("/account/phone/remove",        (Delegate)AccountEndpoints.RemovePhoneAsync).DisableAntiforgery().RequireAuthorization();

        // GET y no POST: lo pide un <a href> de PersonalData.razor. Hace de intermediaria porque el
        // endpoint de la API exige Bearer y el navegador no lo lleva en un enlace normal.
        endpoints.MapGet("/account/personal-data/download", (Delegate)AccountEndpoints.DownloadPersonalDataAsync).RequireAuthorization();

        return endpoints;
    }
}
