namespace MLMConquerorGlobalEdition.SharedKernel.Interfaces;

/// <summary>
/// Abstraction over IDistributedCache with typed JSON serialisation.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken ct = default) where T : class;
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Incrementa un contador y devuelve su valor nuevo. La expiración se fija solo cuando el
    /// contador se crea, para que la ventana no se estire con cada incremento.
    ///
    /// Es atómico cuando hay Redis detrás. Con el respaldo en memoria la atomicidad es solo
    /// dentro del proceso, que es correcto porque ese modo implica una sola instancia.
    /// </summary>
    Task<long> IncrementAsync(string key, TimeSpan expiry, CancellationToken ct = default);
}
