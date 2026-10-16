namespace MLMConquerorGlobalEdition.AdminAPI.Services;

/// <summary>
/// Registro del cliente HTTP "BillingApi": AdminTokenPurchasesController reenvía a Billing confirmar,
/// rechazar, reabrir y conciliar compras de tokens (Plan F) con el Bearer del administrador.
/// </summary>
public static class BillingApiRegistration
{
    public const string HttpClientName        = "BillingApi";
    public const string BaseUrlKey            = "Services:BillingBaseUrl";
    public const string DevelopmentDefaultUrl = "https://localhost:7007/";
    public static readonly TimeSpan Timeout   = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Misma clave, mismas reglas y mismo tiempo que el cliente del BizCenter
    /// (BizCenter/Services/BillingClientRegistration): SIN desactivar la validación del certificado,
    /// porque por aquí viaja un token de administrador que autoriza emitir tokens; 90 s porque
    /// confirmar o conciliar emite y manda el recibo dentro de la petición.
    ///
    /// La URL no va en el appsettings.json base: fuera de Development, sin <see cref="BaseUrlKey"/> el
    /// arranque falla con un mensaje claro en vez de mandar el token del administrador a localhost. En
    /// Development cae a <see cref="DevelopmentDefaultUrl"/>. La BaseAddress siempre acaba en barra: sin
    /// ella, una URL con ruta ("https://host/billing") perdería su último segmento.
    /// </summary>
    public static IServiceCollection AddBillingApiClient(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var baseUrl = configuration[BaseUrlKey];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException(
                    $"{BaseUrlKey} is not configured. AdminAPI forwards token purchase actions to Billing and needs its URL.");
            baseUrl = DevelopmentDefaultUrl;
        }

        baseUrl = baseUrl.Trim();
        if (!baseUrl.EndsWith('/')) baseUrl += "/";
        var baseAddress = new Uri(baseUrl, UriKind.Absolute);

        services.AddHttpClient(HttpClientName, c =>
        {
            c.BaseAddress = baseAddress;
            c.Timeout     = Timeout;
        });
        return services;
    }
}
