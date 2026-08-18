using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Payout.PayQuicker;

public interface IPayQuickerTokenProvider
{
    Task<Result<string>> GetAccessTokenAsync(PayQuickerSettings settings, CancellationToken ct = default);
}

/// <summary>
/// OAuth2 client-credentials contra PayQuicker, con el token cacheado hasta poco antes de
/// que expire.
///
/// A diferencia de la implementación de MWRLife, que pedía un token nuevo en CADA llamada
/// (duplicando la latencia y martillando el endpoint de auth), acá se cachea por
/// (versión, ambiente, clientId) y se renueva con 60 s de margen. El semáforo evita la
/// estampida: si veinte pagos concurrentes encuentran el token vencido, uno solo lo pide.
/// </summary>
public class PayQuickerTokenProvider : IPayQuickerTokenProvider
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>Margen para no usar un token que expira mientras viaja la request.</summary>
    private static readonly TimeSpan ExpirySkew = TimeSpan.FromSeconds(60);

    private readonly IHttpClientFactory                _httpFactory;
    private readonly IMemoryCache                      _cache;
    private readonly ILogger<PayQuickerTokenProvider>  _logger;

    public PayQuickerTokenProvider(
        IHttpClientFactory httpFactory,
        IMemoryCache cache,
        ILogger<PayQuickerTokenProvider> logger)
    {
        _httpFactory = httpFactory;
        _cache       = cache;
        _logger      = logger;
    }

    public async Task<Result<string>> GetAccessTokenAsync(PayQuickerSettings settings, CancellationToken ct = default)
    {
        if (_cache.TryGetValue<string>(settings.TokenCacheKey, out var cached) && !string.IsNullOrEmpty(cached))
            return Result<string>.Success(cached!);

        await Gate.WaitAsync(ct);
        try
        {
            // Otro hilo pudo haberlo renovado mientras esperábamos el semáforo.
            if (_cache.TryGetValue<string>(settings.TokenCacheKey, out var again) && !string.IsNullOrEmpty(again))
                return Result<string>.Success(again!);

            var client = _httpFactory.CreateClient(PayQuickerHttp.ClientName);

            using var request = new HttpRequestMessage(HttpMethod.Post, settings.TokenUrl)
            {
                Content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                    new KeyValuePair<string, string>("scope",      settings.Scopes)
                })
            };

            var basic = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{settings.ClientId}:{settings.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

            using var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                // El body del token puede traer el motivo (invalid_scope, invalid_client);
                // se registra pero NO se propaga al llamador para no filtrar detalles de auth.
                _logger.LogError(
                    "PayQuicker {Version} token request failed ({Status}) at {Url}: {Body}",
                    settings.ApiVersion, (int)response.StatusCode, settings.TokenUrl, body);

                return Result<string>.Failure(
                    "PAYQUICKER_AUTH_FAILED",
                    $"PayQuicker {settings.ApiVersion} authentication failed ({(int)response.StatusCode}).");
            }

            var token = System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(body);
            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
                return Result<string>.Failure(
                    "PAYQUICKER_AUTH_MALFORMED",
                    "PayQuicker returned a token response without an access_token.");

            var lifetime = token.ExpiresIn > 0 ? TimeSpan.FromSeconds(token.ExpiresIn) : TimeSpan.FromMinutes(10);
            var ttl      = lifetime > ExpirySkew ? lifetime - ExpirySkew : lifetime;

            _cache.Set(settings.TokenCacheKey, token.AccessToken, ttl);
            return Result<string>.Success(token.AccessToken!);
        }
        finally
        {
            Gate.Release();
        }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("expires_in")]   public int     ExpiresIn   { get; set; }
        [JsonPropertyName("token_type")]   public string? TokenType   { get; set; }
    }
}
