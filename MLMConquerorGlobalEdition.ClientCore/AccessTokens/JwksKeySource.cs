using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace MLMConquerorGlobalEdition.ClientCore.AccessTokens;

/// <summary>
/// Dónde guarda la aplicación la última copia de las llaves públicas, para poder validar al
/// arrancar sin red. No es un secreto: son llaves públicas.
/// </summary>
public interface IJwksCache
{
    string? Load();
    void Save(string jwksJson);
}

/// <summary>Una caché hecha con dos funciones (en MAUI, <c>Preferences.Get</c> y <c>Preferences.Set</c>).</summary>
public sealed class DelegateJwksCache(Func<string?> load, Action<string> save) : IJwksCache
{
    public string? Load() => load();
    public void Save(string jwksJson) => save(jwksJson);
}

/// <summary>Sin caché persistente: las llaves solo viven en memoria mientras dure el proceso.</summary>
public sealed class NoJwksCache : IJwksCache
{
    public string? Load() => null;
    public void Save(string jwksJson) { }
}

/// <summary>
/// Las llaves públicas con las que SignupAPI firma los tokens, descargadas de su
/// <c>/.well-known/jwks.json</c>.
/// </summary>
/// <remarks>
/// POR QUÉ SE DESCARGAN Y NO VAN DENTRO DE LA APLICACIÓN: con la llave incrustada, cada rotación
/// obligaría a publicar una versión nueva en las tiendas, y hasta que cada usuario actualizara su
/// aplicación no podría entrar. Descargada, una llave nueva llega sola: cuando un token no lo firma
/// ninguna llave conocida, se vuelve a pedir el documento (como mucho una vez por
/// <see cref="MinRefreshInterval"/>, para que un token basura no se convierta en una llamada por
/// pantalla) y se reintenta.
///
/// La descarga va por HTTPS a la misma API que entrega los tokens, así que quien pudiera cambiar
/// estas llaves en el camino también podría cambiar los tokens: no se confía en nada que no se
/// confiara ya.
/// </remarks>
public sealed class JwksKeySource
{
    public const string HttpClientName = "Jwks";
    public const string DocumentPath   = ".well-known/jwks.json";

    public static readonly TimeSpan MinRefreshInterval = TimeSpan.FromMinutes(1);

    private readonly IHttpClientFactory     _httpClientFactory;
    private readonly IJwksCache             _cache;
    private readonly ILogger<JwksKeySource> _logger;
    private readonly TimeProvider           _time;
    private readonly SemaphoreSlim          _gate = new(1, 1);

    private IReadOnlyList<SecurityKey> _keys = [];
    private bool            _cacheLoaded;
    private DateTimeOffset? _lastFetch;

    public JwksKeySource(
        IHttpClientFactory     httpClientFactory,
        IJwksCache             cache,
        ILogger<JwksKeySource> logger,
        TimeProvider?          time = null)
    {
        _httpClientFactory = httpClientFactory;
        _cache             = cache;
        _logger            = logger;
        _time              = time ?? TimeProvider.System;
    }

    /// <summary>
    /// Las llaves conocidas. La primera vez salen de la caché; si no hay, se descargan.
    /// </summary>
    public async Task<IReadOnlyList<SecurityKey>> GetKeysAsync(CancellationToken ct = default)
    {
        if (_keys.Count > 0) return _keys;

        await _gate.WaitAsync(ct);
        try
        {
            if (_keys.Count > 0) return _keys;

            if (!_cacheLoaded)
            {
                _cacheLoaded = true;
                var cached = Parse(SafeLoad());
                if (cached.Count > 0)
                {
                    _keys = cached;
                    return _keys;
                }
            }

            await FetchLockedAsync(ct);
            return _keys;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Vuelve a descargar las llaves si hace al menos <see cref="MinRefreshInterval"/> de la última
    /// descarga. Devuelve true si se descargaron; false si no tocaba o la API no respondió.
    /// </summary>
    public async Task<bool> TryRefreshAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_lastFetch is { } last && _time.GetUtcNow() - last < MinRefreshInterval)
                return false;

            return await FetchLockedAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<bool> FetchLockedAsync(CancellationToken ct)
    {
        _lastFetch = _time.GetUtcNow();
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var json   = await client.GetStringAsync(DocumentPath, ct);
            var keys   = Parse(json);
            if (keys.Count == 0)
            {
                _logger.LogWarning("El documento de llaves de la API no trae ninguna llave de firma.");
                return false;
            }

            _keys = keys;
            try { _cache.Save(json); }
            catch (Exception ex) { _logger.LogWarning(ex, "No se pudo guardar la copia de las llaves públicas."); }
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Sin red se sigue con lo que hubiera: un token firmado con una llave conocida se puede
            // validar igual, y uno que no, se queda fuera hasta que vuelva la conexión.
            _logger.LogWarning(ex, "No se pudieron descargar las llaves públicas de la API.");
            return false;
        }
    }

    private string? SafeLoad()
    {
        try { return _cache.Load(); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer la copia de las llaves públicas.");
            return null;
        }
    }

    /// <summary>Las llaves de firma RSA de un documento JWKS; vacío si no se puede leer.</summary>
    public static IReadOnlyList<SecurityKey> Parse(string? jwksJson)
    {
        if (string.IsNullOrWhiteSpace(jwksJson)) return [];
        try
        {
            return new JsonWebKeySet(jwksJson).Keys
                .Where(k => k.Kty == JsonWebAlgorithmsKeyTypes.RSA
                         && (string.IsNullOrEmpty(k.Use) || k.Use == JsonWebKeyUseNames.Sig))
                .Cast<SecurityKey>()
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }
}
