using Hangfire;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Jobs;

/// <summary>
/// Trabajo recurrente de Hangfire — cada hora, cola "billing". Caduca las compras de tokens en cripto
/// que nadie confirmó en Billing:TokenPurchase:CryptoExpiryDays días (3 por defecto; 0 lo apaga) y
/// libera su cupo (D18). Idempotente: solo toca las que siguen esperando.
/// </summary>
[Queue("billing")]
public class TokenPurchaseCryptoExpiryJob
{
    private const string Actor = "token-purchase-crypto-expiry";

    private readonly ITokenPurchaseService _purchases;
    private readonly IConfiguration _config;
    private readonly ILogger<TokenPurchaseCryptoExpiryJob> _logger;

    public TokenPurchaseCryptoExpiryJob(
        ITokenPurchaseService purchases, IConfiguration config, ILogger<TokenPurchaseCryptoExpiryJob> logger)
    {
        _purchases = purchases;
        _config    = config;
        _logger    = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        // Se lee en cada corrida, no al arrancar: cambiar el plazo no exige reiniciar Billing.
        var days = _config.GetValue(TokenPurchaseRules.CryptoExpiryDaysConfigKey, TokenPurchaseRules.DefaultCryptoExpiryDays);
        var expired = await _purchases.ExpireStaleCryptoAsync(days, Actor, ct);

        if (expired > 0)
            _logger.LogInformation("TokenPurchaseCryptoExpiryJob: {Count} compras en cripto caducadas tras {Days} días.", expired, days);
    }
}
