using Microsoft.Extensions.DependencyInjection;
using MLMConquerorGlobalEdition.Repository.Services.Payout.EWallet;
using MLMConquerorGlobalEdition.Repository.Services.Payout.PayQuicker;
using MLMConquerorGlobalEdition.Repository.Services.Payout.Volet;
using MLMConquerorGlobalEdition.Repository.Services.Wallets;

namespace MLMConquerorGlobalEdition.Repository.Services.Payout;

public static class PayoutGatewayServiceCollectionExtensions
{
    /// <summary>
    /// Registra SÓLO lo necesario para hablar con los proveedores de payout: cifrado de
    /// credenciales, los gateways, sus clientes HTTP y el registrador de cuentas.
    ///
    /// Vive en Repository, no en Billing, porque BizCenter también lo necesita: cuando un
    /// ambassador elige o cambia su método de cobro hay que darlo de alta en el proveedor.
    /// Si esto viviera en Billing, BizCenter —el API que usa el ambassador— tendría que
    /// referenciar al proyecto de facturación y arrastrar Stripe e iText que nunca usa.
    ///
    /// Billing y AdminAPI llaman a AddPayoutServices, que además suma el orquestador, los
    /// recibos, la exportación de lotes y la reconciliación.
    /// </summary>
    public static IServiceCollection AddPayoutGatewayClients(this IServiceCollection services)
    {
        // Cifrado de las credenciales de gateway: key ring en la base compartida, envuelto
        // con certificado X.509. Ver GatewayCredentialProtectionExtensions.
        services.AddGatewayCredentialProtection();

        services.AddScoped<IPayoutGatewayService, EWalletPayoutGatewayService>();
        services.AddScoped<IPayoutGatewayService, VoletPayoutGatewayService>();
        services.AddScoped<IPayoutGatewayService, PayQuickerPayoutGatewayService>();
        services.AddScoped<IPayoutGatewayResolver, PayoutGatewayResolver>();

        // Da de alta la cuenta del miembro en el proveedor al registrar o cambiar el método
        // de cobro. Lo consume MemberWalletService.
        services.AddScoped<IPayoutAccountRegistrar, GatewayPayoutAccountRegistrar>();

        // ── PayQuicker ──────────────────────────────────────────────────────
        // Se registran AMBOS clientes; cuál se usa lo decide en runtime el selector
        // ApiVersion de PaymentGatewayInfo, resuelto por PayQuickerSettingsProvider.
        services.AddMemoryCache();                       // respalda el caché del token
        services.AddScoped<IPayQuickerSettingsProvider, PayQuickerSettingsProvider>();
        services.AddSingleton<IPayQuickerTokenProvider, PayQuickerTokenProvider>();
        services.AddScoped<IPayQuickerClient, PayQuickerV1Client>();
        services.AddScoped<IPayQuickerClient, PayQuickerV2Client>();

        // Un solo HttpClient nombrado para toda la integración. Con IHttpClientFactory los
        // headers van por request, así que no hay estado compartido que dos pagos
        // concurrentes puedan pisarse — el bug que arrastra la integración de MWRLife.
        services.AddHttpClient(PayQuickerHttp.ClientName, c =>
        {
            c.Timeout = TimeSpan.FromSeconds(30);
        });

        // ── eWallet (i-Payout) ──────────────────────────────────────────────
        services.AddScoped<IEWalletClient, EWalletClient>();
        services.AddHttpClient(EWalletClient.HttpClientName, c =>
        {
            c.Timeout = TimeSpan.FromSeconds(30);
        });

        // ── Volet (ex AdvCash) ──────────────────────────────────────────────
        // SOAP, no REST. El envelope se arma a mano sobre HttpClient.
        services.AddScoped<IVoletClient, VoletClient>();
        services.AddHttpClient(VoletClient.HttpClientName, c =>
        {
            c.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}
