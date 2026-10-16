namespace MLMConquerorGlobalEdition.BizCenter.Services;

/// <summary>Registro del cliente HTTP con el que el BizCenter llama a Billing (compra de tokens, Plan F).</summary>
public static class BillingClientRegistration
{
    public const string BaseUrlKey             = "Services:BillingBaseUrl";
    public const string DevelopmentDefaultUrl  = "https://localhost:7007/";
    public static readonly TimeSpan Timeout    = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Reenvía el JWT del miembro y, en la ruta de pago con saldo, el permiso de step-up. SIN desactivar
    /// la validación del certificado: por aquí viaja el token del miembro que autoriza mover su dinero.
    /// 90 s porque una compra con tarjeta puede probar varios procesadores y una con eWallet llama dos
    /// veces a i-Payout. Sin reintentos automáticos: el reintento lo hace la persona con la misma clave
    /// de idempotencia.
    ///
    /// Fuera de Development no hay valor por defecto: sin <see cref="BaseUrlKey"/> el arranque falla con
    /// un mensaje claro en vez de mandar el token del miembro a localhost. La BaseAddress siempre acaba
    /// en barra: sin ella, una URL con ruta ("https://host/billing") perdería su último segmento al
    /// combinarse con las rutas relativas del cliente.
    /// </summary>
    public static IServiceCollection AddBillingClient(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var baseUrl = configuration[BaseUrlKey];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException(
                    $"{BaseUrlKey} is not configured. BizCenter forwards token purchases to Billing and needs its URL.");
            baseUrl = DevelopmentDefaultUrl;
        }

        baseUrl = baseUrl.Trim();
        if (!baseUrl.EndsWith('/')) baseUrl += "/";
        var baseAddress = new Uri(baseUrl, UriKind.Absolute);

        services.AddHttpClient(BillingClient.HttpClientName, c =>
        {
            c.BaseAddress = baseAddress;
            c.Timeout     = Timeout;
        });
        services.AddScoped<IBillingClient, BillingClient>();
        return services;
    }
}
