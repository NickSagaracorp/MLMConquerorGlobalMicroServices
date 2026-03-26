using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SharedKernel.Services;

/// <summary>
/// IDistributedCache wrapper with JSON serialisation.
/// Register as Singleton after calling AddStackExchangeRedisCache (or AddDistributedMemoryCache for tests).
/// </summary>
public class CacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public CacheService(IDistributedCache cache) => _cache = cache;

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        var bytes = await _cache.GetAsync(key, ct);
        if (bytes is null) return null;
        return JsonSerializer.Deserialize<T>(bytes, _jsonOptions);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken ct = default)
        where T : class
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, _jsonOptions);
        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiry };
        await _cache.SetAsync(key, bytes, options, ct);
    }

    public Task RemoveAsync(string key, CancellationToken ct = default) =>
        _cache.RemoveAsync(key, ct);
}
