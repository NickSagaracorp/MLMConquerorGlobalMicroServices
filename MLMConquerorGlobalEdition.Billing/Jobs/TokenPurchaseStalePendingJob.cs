using Hangfire;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Jobs;

/// <summary>
/// Trabajo recurrente de Hangfire — cada hora, cola "billing". Red de seguridad de la compra de tokens:
/// una compra con tarjeta o eWallet que lleva Billing:TokenPurchase:StalePendingMinutes (30 por
/// defecto; 0 lo apaga) en Pending es que el proceso murió, o no pudo guardar, entre el cobro y la
/// emisión. La pasa a conciliar para que tesorería mire el gateway; NUNCA la libera, porque el dinero
/// pudo moverse (R5). Idempotente: solo toca las que siguen en Pending.
/// </summary>
[Queue("billing")]
public class TokenPurchaseStalePendingJob
{
    private const string Actor = "token-purchase-stale-pending";

    private readonly ITokenPurchaseService _purchases;
    private readonly IConfiguration _config;
    private readonly ILogger<TokenPurchaseStalePendingJob> _logger;

    public TokenPurchaseStalePendingJob(
        ITokenPurchaseService purchases, IConfiguration config, ILogger<TokenPurchaseStalePendingJob> logger)
    {
        _purchases = purchases;
        _config    = config;
        _logger    = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        // Se lee en cada corrida, no al arrancar: cambiar el plazo no exige reiniciar Billing.
        var minutes = _config.GetValue(TokenPurchaseRules.StalePendingMinutesConfigKey, TokenPurchaseRules.DefaultStalePendingMinutes);
        var moved = await _purchases.MoveStalePendingToReconciliationAsync(minutes, Actor, ct);

        if (moved > 0)
            _logger.LogWarning("TokenPurchaseStalePendingJob: {Count} compras de tokens llevaban más de {Minutes} min en Pending y pasan a conciliar.",
                moved, minutes);
    }
}
