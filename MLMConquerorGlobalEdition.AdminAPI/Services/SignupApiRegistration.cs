namespace MLMConquerorGlobalEdition.AdminAPI.Services;

/// <summary>
/// Registro del cliente HTTP "SignupApi": AdminCryptoSignupsController (confirmar cripto),
/// AdminEntraxSignupsController (Re-check) y AdminSignupsController (reintentar altas) reenvían a
/// SignupAPI, los dos primeros con el Bearer del administrador.
/// </summary>
public static class SignupApiRegistration
{
    public const string HttpClientName        = "SignupApi";
    public const string BaseUrlKey            = "Services:SignupAPI";
    public const string DevelopmentDefaultUrl = "https://localhost:7005/";

    /// <summary>
    /// Tope del cliente entero. Confirmar una cripto o reintentar un alta activan y generan las
    /// comisiones dentro de la petición, así que no puede ser tan corto como el del Re-check de Entrax,
    /// que pone el suyo (20 s) por petición.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Mismas reglas que <see cref="BillingApiRegistration"/>: por aquí viaja un token de administrador
    /// que activa membresías, así que fuera de Development se valida el certificado y sin
    /// <see cref="BaseUrlKey"/> el arranque falla en vez de mandar el token a localhost. En Development
    /// cae a <see cref="DevelopmentDefaultUrl"/> y acepta el certificado de desarrollo (como antes).
    /// La BaseAddress siempre acaba en barra para no perder el último segmento de una URL con ruta.
    /// </summary>
    public static IServiceCollection AddSignupApiClient(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var isDevelopment = environment.IsDevelopment();

        var baseUrl = configuration[BaseUrlKey];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            if (!isDevelopment)
                throw new InvalidOperationException(
                    $"{BaseUrlKey} is not configured. AdminAPI forwards signup payment actions to SignupAPI and needs its URL.");
            baseUrl = DevelopmentDefaultUrl;
        }

        baseUrl = baseUrl.Trim();
        if (!baseUrl.EndsWith('/')) baseUrl += "/";
        var baseAddress = new Uri(baseUrl, UriKind.Absolute);

        var builder = services.AddHttpClient(HttpClientName, c =>
        {
            c.BaseAddress = baseAddress;
            c.Timeout     = Timeout;
        });

        if (isDevelopment)
            builder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });

        return services;
    }
}
