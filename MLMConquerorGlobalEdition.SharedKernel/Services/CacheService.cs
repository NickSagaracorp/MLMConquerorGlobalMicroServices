using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using StackExchange.Redis;

namespace MLMConquerorGlobalEdition.SharedKernel.Services;

/// <summary>
/// IDistributedCache wrapper with JSON serialisation. Resilient to backend
/// failures: if Redis (or whatever backend) is unreachable, every operation
/// degrades to a cache miss / no-op rather than propagating the exception.
/// Register as Singleton after calling AddStackExchangeRedisCache (or
/// AddDistributedMemoryCache for tests).
///
/// El <see cref="IConnectionMultiplexer"/> es opcional y solo lo usa
/// <see cref="IncrementAsync"/>: IDistributedCache no expone INCR, así que sin él un contador
/// solo puede simularse con leer-modificar-escribir. Los hosts que alojan límites de seguridad
/// (SignupAPI, AdminAPI) lo registran; el resto se queda con el respaldo por proceso.
/// </summary>
public class CacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheService>? _logger;
    private readonly IConnectionMultiplexer? _multiplexer;

    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Cerrojos por franjas para el respaldo sin Redis. Franjas fijas y no un diccionario por
    /// clave: un diccionario indexado por clave crece con cada challenge y cada usuario que
    /// pasa por aquí, y limpiarlo exige saber cuándo expiró el contador —que es justo lo que
    /// esta clase no sabe—. Con franjas el consumo está acotado por construcción; dos claves
    /// distintas que caigan en la misma franja solo se serializan entre sí, que es correcto
    /// aunque sea algo más lento.
    /// </summary>
    private static readonly SemaphoreSlim[] _stripes =
        Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    /// <summary>0 mientras no se haya avisado de que el incremento no es atómico entre instancias.</summary>
    private int _nonAtomicWarningIssued;

    public CacheService(
        IDistributedCache cache,
        ILogger<CacheService>? logger = null,
        IConnectionMultiplexer? multiplexer = null)
    {
        _cache       = cache;
        _logger      = logger;
        _multiplexer = multiplexer;
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

    /// <inheritdoc/>
    public async Task<long> IncrementAsync(string key, TimeSpan expiry, CancellationToken ct = default)
    {
        if (_multiplexer is not null)
        {
            try
            {
                var db    = _multiplexer.GetDatabase();
                var value = await db.StringIncrementAsync(key);

                // Solo al crear el contador. Renovar el TTL en cada incremento convertiría
                // "3 cada 15 minutos" en "3 y luego 15 minutos de silencio" — la ventana se
                // estiraría sola mientras siguieran llegando peticiones.
                if (value == 1)
                    await db.KeyExpireAsync(key, expiry);

                return value;
            }
            catch (Exception ex)
            {
                // Degradar como el resto de la clase, pero dejando constancia: mientras Redis
                // no responda el tope solo vale dentro de este proceso.
                _logger?.LogWarning(
                    ex, "Cache IncrementAsync via Redis failed for key {Key}; falling back to in-process counter.", key);
            }
        }
        else if (Interlocked.Exchange(ref _nonAtomicWarningIssued, 1) == 0)
        {
            _logger?.LogWarning(
                "CacheService has no IConnectionMultiplexer registered: IncrementAsync is atomic only " +
                "within this process. Any counter used as a security limit (2FA attempts, issue windows) " +
                "can be exceeded when more than one instance is running. Register IConnectionMultiplexer " +
                "as a singleton to get Redis INCR.");
        }

        return await IncrementInProcessAsync(key, expiry, ct);
    }

    /// <inheritdoc/>
    public async Task<long> DecrementAsync(string key, CancellationToken ct = default)
    {
        if (_multiplexer is not null)
        {
            try
            {
                // Sin KeyExpire: devolver un cupo no debe alargar ni reiniciar la ventana.
                // Si la clave ya expiró, DECR la crearía en -1; se limpia para no dejar un
                // contador negativo que luego permitiría más emisiones de la cuenta.
                var db    = _multiplexer.GetDatabase();
                var value = await db.StringDecrementAsync(key);

                if (value <= 0)
                    await db.KeyDeleteAsync(key);

                return value;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(
                    ex, "Cache DecrementAsync via Redis failed for key {Key}; falling back to in-process counter.", key);
            }
        }

        return await DecrementInProcessAsync(key, ct);
    }

    /// <summary>
    /// Respaldo sin Redis para <see cref="DecrementAsync"/>. Conserva el vencimiento que ya
    /// tenía el contador, por la misma razón que el incremento: recalcularlo desde ahora
    /// estiraría la ventana.
    /// </summary>
    private async Task<long> DecrementInProcessAsync(string key, CancellationToken ct)
    {
        var gate = _stripes[(uint)StringComparer.Ordinal.GetHashCode(key) % _stripes.Length];

        await gate.WaitAsync(ct);
        try
        {
            var now = DateTimeOffset.UtcNow;

            byte[]? bytes;
            try
            {
                bytes = await _cache.GetAsync(key, ct);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Cache DecrementAsync read failed for key {Key}; ignoring.", key);
                return 0;
            }

            if (bytes is null || !TryParseCounter(bytes, out var count, out var expiresAt) || expiresAt <= now)
                return 0;

            var next = count - 1;

            try
            {
                if (next <= 0)
                {
                    await _cache.RemoveAsync(key, ct);
                    return 0;
                }

                var remaining = expiresAt - now;
                if (remaining < TimeSpan.FromSeconds(1))
                    remaining = TimeSpan.FromSeconds(1);

                await _cache.SetAsync(
                    key,
                    FormatCounter(next, expiresAt),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = remaining },
                    ct);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Cache DecrementAsync write failed for key {Key}; ignoring.", key);
            }

            return next;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Respaldo sin Redis: el mismo leer-modificar-escribir de siempre, pero bajo un cerrojo,
    /// de modo que dentro del proceso sí es un incremento.
    ///
    /// El valor guardado es <c>"cuenta|instanteDeVencimientoUtcEnTicks"</c>. Lleva su propio
    /// vencimiento porque <c>IDistributedCache.SetAsync</c> no deja tocar el valor sin
    /// reescribir las opciones: si el TTL se recalculara desde ahora en cada incremento, la
    /// ventana se estiraría sola mientras siguieran llegando peticiones, y "3 cada 15 minutos"
    /// se convertiría en "3 y luego 15 minutos de silencio". Con el vencimiento dentro del
    /// valor, el TTL que se reescribe es siempre el que le quedaba al contador original.
    /// </summary>
    private async Task<long> IncrementInProcessAsync(string key, TimeSpan expiry, CancellationToken ct)
    {
        var gate = _stripes[(uint)StringComparer.Ordinal.GetHashCode(key) % _stripes.Length];

        await gate.WaitAsync(ct);
        try
        {
            var now = DateTimeOffset.UtcNow;

            long           current  = 0;
            DateTimeOffset expiresAt = now + expiry;

            try
            {
                var bytes = await _cache.GetAsync(key, ct);
                if (bytes is not null && TryParseCounter(bytes, out var storedCount, out var storedExpiry)
                                      && storedExpiry > now)
                {
                    current   = storedCount;
                    expiresAt = storedExpiry;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Cache IncrementAsync read failed for key {Key}; restarting counter.", key);
            }

            var next      = current + 1;
            var remaining = expiresAt - now;
            if (remaining < TimeSpan.FromSeconds(1))
                remaining = TimeSpan.FromSeconds(1);

            try
            {
                await _cache.SetAsync(
                    key,
                    FormatCounter(next, expiresAt),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = remaining },
                    ct);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Cache IncrementAsync write failed for key {Key}; ignoring.", key);
            }

            return next;
        }
        finally
        {
            gate.Release();
        }
    }

    private static byte[] FormatCounter(long count, DateTimeOffset expiresAt) =>
        Encoding.UTF8.GetBytes(string.Create(
            CultureInfo.InvariantCulture, $"{count}|{expiresAt.UtcTicks}"));

    private static bool TryParseCounter(byte[] bytes, out long count, out DateTimeOffset expiresAt)
    {
        count     = 0;
        expiresAt = default;

        var raw       = Encoding.UTF8.GetString(bytes);
        var separator = raw.IndexOf('|');
        if (separator <= 0) return false;

        if (!long.TryParse(raw.AsSpan(0, separator), NumberStyles.Integer,
                           CultureInfo.InvariantCulture, out count))
            return false;

        if (!long.TryParse(raw.AsSpan(separator + 1), NumberStyles.Integer,
                           CultureInfo.InvariantCulture, out var ticks)
            || ticks < 0 || ticks > DateTimeOffset.MaxValue.UtcTicks)
            return false;

        expiresAt = new DateTimeOffset(ticks, TimeSpan.Zero);
        return true;
    }
}
