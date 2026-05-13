using Hangfire;
using MLMConquerorGlobalEdition.Billing.Services.Routing;

namespace MLMConquerorGlobalEdition.Billing.Jobs;

/// <summary>
/// Hangfire recurring job — hourly, queue "billing".
/// Refreshes exchange rates for all configured currencies and persists fresh
/// ExchangeRateSnapshot rows. Also warms the Redis cache so the next hour's
/// charges use a live rate.
/// </summary>
[Queue("billing")]
public class ExchangeRateRefreshJob
{
    private static readonly string[] TargetCurrencies = { "EUR", "GBP", "CAD", "AUD" };

    private readonly ICurrencyConversionService _currencyConversion;
    private readonly ILogger<ExchangeRateRefreshJob> _logger;

    public ExchangeRateRefreshJob(
        ICurrencyConversionService currencyConversion,
        ILogger<ExchangeRateRefreshJob> logger)
    {
        _currencyConversion = currencyConversion;
        _logger             = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("ExchangeRateRefreshJob: starting refresh for {Count} currencies.", TargetCurrencies.Length);

        foreach (var currency in TargetCurrencies)
        {
            try
            {
                // GetRateAsync handles: Redis cache miss → live API call → DB persist
                var result = await _currencyConversion.GetRateAsync(currency, ct);
                if (result.IsSuccess)
                    _logger.LogInformation("ExchangeRateRefreshJob: {Currency} rate = {Rate}", currency, result.Value);
                else
                    _logger.LogWarning("ExchangeRateRefreshJob: failed to refresh {Currency}: {Error}", currency, result.Error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExchangeRateRefreshJob: unhandled error refreshing {Currency}.", currency);
            }
        }

        _logger.LogInformation("ExchangeRateRefreshJob: completed.");
    }
}
