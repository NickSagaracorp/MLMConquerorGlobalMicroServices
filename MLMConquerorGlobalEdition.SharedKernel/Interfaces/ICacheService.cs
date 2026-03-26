namespace MLMConquerorGlobalEdition.SharedKernel.Interfaces;

/// <summary>
/// Abstraction over IDistributedCache with typed JSON serialisation.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken ct = default) where T : class;
    Task RemoveAsync(string key, CancellationToken ct = default);
}
