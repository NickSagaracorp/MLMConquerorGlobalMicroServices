using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SharedKernel.Services;

/// <summary>
/// IDistributedCache wrapper with JSON serialisation. Resilient to backend
/// failures: if Redis (or whatever backend) is unreachable, every operation
/// degrades to a cache miss / no-op rather than propagating the exception.
/// Register as Singleton after calling AddStackExchangeRedisCache (or
/// AddDistributedMemoryCache for tests).
/// </summary>
public class CacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheService>? _logger;

    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public CacheService(IDistributedCache cache, ILogger<CacheService>? logger = null)
    {
        _cache  = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        try
        {
            var bytes = await _cache.GetAsync(key, ct);
            if (bytes is null) return null;
            return JsonSerializer.Deserialize<T>(bytes, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Cache GetAsync failed for key {Key}; treating as miss.", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken ct = default)
        where T : class
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, _jsonOptions);
            var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiry };
            await _cache.SetAsync(key, bytes, options, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Cache SetAsync failed for key {Key}; ignoring.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _cache.RemoveAsync(key, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Cache RemoveAsync failed for key {Key}; ignoring.", key);
        }
    }
}
