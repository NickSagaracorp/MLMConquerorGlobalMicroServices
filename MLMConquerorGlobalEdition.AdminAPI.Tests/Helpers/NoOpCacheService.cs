using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;

/// <summary>
/// Test double for <see cref="ICacheService"/> that always misses (returns null on Get)
/// and no-ops on Set/Remove — so handlers under test execute their real computation path.
/// </summary>
public sealed class NoOpCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
        => Task.FromResult<T?>(null);

    public Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken ct = default) where T : class
        => Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>Cada llamada es la primera: nada de estado entre pruebas.</summary>
    public Task<long> IncrementAsync(string key, TimeSpan expiry, CancellationToken ct = default)
        => Task.FromResult(1L);

    public Task<long> DecrementAsync(string key, CancellationToken ct = default)
        => Task.FromResult(0L);
}
