using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Routing;

/// <summary>
/// Currency conversion using currencyconverterapi.com.
///   - Rate cached in Redis with 1-hour TTL (key: exchange:USD:{currency}).
///   - On cache miss / Redis unavailable: call the API, then cache + persist snapshot.
///   - On API failure: read the most-recent ExchangeRateSnapshot from the DB.
///   - Applies CurrencyPolicy.MarkupPercent on top of the spot rate.
/// </summary>
public class CurrencyConversionService : ICurrencyConversionService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);
    private const string CacheKeyPrefix = "exchange:USD:";
    private const string CredentialKey  = "CurrencyConverterApi";

    private readonly AppDbContext          _db;
    private readonly IDistributedCache     _cache;
    private readonly IHttpClientFactory    _httpClientFactory;
    private readonly IDateTimeProvider     _dateTime;
    private readonly ILogger<CurrencyConversionService> _logger;

    public CurrencyConversionService(
        AppDbContext db,
        IDistributedCache cache,
        IHttpClientFactory httpClientFactory,
        IDateTimeProvider dateTime,
        ILogger<CurrencyConversionService> logger)
    {
        _db                = db;
        _cache             = cache;
        _httpClientFactory = httpClientFactory;
        _dateTime          = dateTime;
        _logger            = logger;
    }

    public async Task<Result<decimal>> ConvertAsync(
        decimal amountUsd, string targetCurrency, decimal markupPercent, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(targetCurrency) || targetCurrency.Equals("USD", StringComparison.OrdinalIgnoreCase))
            return Result<decimal>.Success(amountUsd);

        var rateResult = await GetRateAsync(targetCurrency, ct);
        if (!rateResult.IsSuccess)
            return Result<decimal>.Failure(rateResult.ErrorCode!, rateResult.Error!);

        var rate     = rateResult.Value;
        var markup   = 1m + (markupPercent / 100m);
        var converted = Math.Round(amountUsd * rate * markup, 2);
        return Result<decimal>.Success(converted);
    }

    public async Task<Result<decimal>> GetRateAsync(string targetCurrency, CancellationToken ct = default)
    {
        var cacheKey = CacheKeyPrefix + targetCurrency.ToUpperInvariant();

        // 1. Try Redis cache
        try
        {
            var cached = await _cache.GetStringAsync(cacheKey, ct);
            if (cached is not null && decimal.TryParse(cached, out var cachedRate))
                return Result<decimal>.Success(cachedRate);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CurrencyConversionService: Redis unavailable for key {Key}. Falling back.", cacheKey);
        }

        // 2. Try live API
        try
        {
            var apiResult = await FetchFromApiAsync(targetCurrency, ct);
            if (apiResult.IsSuccess)
            {
                var rate = apiResult.Value;
                await CacheAndPersistAsync(cacheKey, targetCurrency, rate, ct);
                return Result<decimal>.Success(rate);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CurrencyConversionService: API call failed for {Currency}. Falling back to DB snapshot.", targetCurrency);
        }

        // 3. DB snapshot fallback
        var snapshot = await _db.ExchangeRateSnapshots
            .AsNoTracking()
            .Where(s => s.QuoteCurrency == targetCurrency.ToUpperInvariant())
            .OrderByDescending(s => s.FetchedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (snapshot is not null)
        {
            _logger.LogWarning(
                "CurrencyConversionService: Using stale DB snapshot for {Currency} from {FetchedAt}.",
                targetCurrency, snapshot.FetchedAtUtc);
            return Result<decimal>.Success(snapshot.Rate);
        }

        return Result<decimal>.Failure(
            "EXCHANGE_RATE_UNAVAILABLE",
            $"Exchange rate for {targetCurrency} is not available from cache, API, or DB snapshot.");
    }

    private async Task<Result<decimal>> FetchFromApiAsync(string targetCurrency, CancellationToken ct)
    {
        var credential = await _db.ApiCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ServiceKey == CredentialKey && c.IsActive, ct);

        if (credential?.ApiKeyEncrypted is null)
            return Result<decimal>.Failure("CURRENCY_API_KEY_MISSING", "CurrencyConverterApi credential not configured.");

        // Decrypt the API key (strip "ENC:" prefix — in a real implementation this
        // would call a proper decryption service; here we trim the prefix for the stub)
        var apiKey = credential.ApiKeyEncrypted.StartsWith("ENC:")
            ? credential.ApiKeyEncrypted[4..]
            : credential.ApiKeyEncrypted;

        var pair     = $"USD_{targetCurrency.ToUpperInvariant()}";
        var baseUrl  = credential.BaseUrl ?? "https://free.currconv.com";
        var url      = $"{baseUrl}/api/v7/convert?q={pair}&compact=ultra&apiKey={apiKey}";

        var client   = _httpClientFactory.CreateClient("CurrencyConverter");
        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json     = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty(pair, out var rateElement) &&
            rateElement.TryGetDecimal(out var rate))
        {
            return Result<decimal>.Success(rate);
        }

        return Result<decimal>.Failure("CURRENCY_API_PARSE_ERROR", $"Could not parse exchange rate for {pair}.");
    }

    private async Task CacheAndPersistAsync(
        string cacheKey, string targetCurrency, decimal rate, CancellationToken ct)
    {
        var now = _dateTime.Now;

        // Cache in Redis
        try
        {
            await _cache.SetStringAsync(
                cacheKey,
                rate.ToString("G"),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl },
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CurrencyConversionService: Failed to set Redis cache for {Key}.", cacheKey);
        }

        // Persist snapshot
        _db.ExchangeRateSnapshots.Add(new ExchangeRateSnapshot
        {
            BaseCurrency  = "USD",
            QuoteCurrency = targetCurrency.ToUpperInvariant(),
            Rate          = rate,
            FetchedAtUtc  = now,
            ExpiresAtUtc  = now.Add(CacheTtl),
            CreatedBy     = "billing-engine",
            CreationDate  = now
        });

        try { await _db.SaveChangesAsync(ct); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CurrencyConversionService: Failed to persist exchange rate snapshot.");
        }
    }
}
