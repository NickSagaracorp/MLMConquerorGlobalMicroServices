using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Routing;

public interface ICurrencyConversionService
{
    /// <summary>
    /// Converts <paramref name="amountUsd"/> to the target currency, applying the
    /// policy markup percentage. Returns the converted amount in the target currency.
    /// </summary>
    Task<Result<decimal>> ConvertAsync(
        decimal amountUsd,
        string  targetCurrency,
        decimal markupPercent,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the current spot rate (USD → targetCurrency) without markup.
    /// Used by the ExchangeRateRefreshJob.
    /// </summary>
    Task<Result<decimal>> GetRateAsync(
        string targetCurrency,
        CancellationToken ct = default);
}
