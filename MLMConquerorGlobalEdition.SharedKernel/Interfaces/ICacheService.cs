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

    /// <summary>
    /// Deshace un incremento y devuelve el valor nuevo. No crea el contador si no existe ni
    /// toca su expiración: solo devuelve un cupo que se apuntó y al final no se gastó.
    ///
    /// Existe porque un contador que sirve de tope tiene que apuntarse <b>antes</b> de la
    /// acción, no después: comprobar sobre un valor leído y escribir al terminar deja el
    /// mismo hueco de concurrencia que el incremento atómico venía a cerrar. El precio es
    /// que una acción que falla después de apuntarse ya ha gastado cupo, y esto lo devuelve.
    ///
    /// Devolver el cupo solo es seguro cuando el fallo impidió que la acción ocurriera —un
    /// SMS que no llegó a enviarse—; quien ataca no controla que el proveedor se caiga, así
    /// que no puede provocarlo para saltarse el tope.
    /// </summary>
    Task<long> DecrementAsync(string key, CancellationToken ct = default);
}
