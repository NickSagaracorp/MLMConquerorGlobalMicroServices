using System.Net.Http.Headers;
using System.Net.Http.Json;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.BizCenter.Services;

/// <inheritdoc cref="IBillingClient"/>
/// <remarks>
/// SIN REINTENTOS AUTOMÁTICOS: si la compra se corta, reintenta la persona con la MISMA clave de
/// idempotencia y Billing devuelve la compra que ya hizo.
///
/// QUÉ SE DEVUELVE TAL CUAL. Un 4xx de Billing con su código (400 CARD_PAYMENTS_DISABLED, 409
/// IDEMPOTENCY_KEY_REUSED, 403 STEP_UP_REQUIRED…): son respuestas definitivas sobre esa clave y la
/// pantalla las traduce. Lo que NO es una respuesta de Billing (no contestó, se agotó el tiempo, un 5xx,
/// un 401 o un cuerpo ilegible) es 502 BILLING_UNAVAILABLE: no se sabe si la compra llegó a hacerse y
/// la pantalla debe reintentar con la misma clave, nunca con una nueva.
/// </remarks>
public sealed class BillingClient : IBillingClient
{
    public const string HttpClientName = "billing";

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<BillingClient> _logger;

    public BillingClient(IHttpClientFactory factory, ILogger<BillingClient> logger)
    {
        _factory = factory;
        _logger  = logger;
    }

    public Task<BillingResult<TokenCatalogDto>> GetTokenCatalogAsync(string bearerToken, CancellationToken ct = default) =>
        SendAsync<TokenCatalogDto>(new HttpRequestMessage(HttpMethod.Get, "api/v1/billing/tokens/catalog"), bearerToken, ct);

    public Task<BillingResult<TokenPurchaseFundsDto>> GetPurchaseFundsAsync(string bearerToken, CancellationToken ct = default) =>
        SendAsync<TokenPurchaseFundsDto>(new HttpRequestMessage(HttpMethod.Get, "api/v1/billing/tokens/funds"), bearerToken, ct);

    public Task<BillingResult<TokenPurchaseResultDto>> PurchaseTokensAsync(
        PurchaseTokensRequest request, string bearerToken, string? stepUpGrant = null, CancellationToken ct = default)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "api/v1/billing/tokens/purchase") { Content = JsonContent.Create(request) };
        if (!string.IsNullOrWhiteSpace(stepUpGrant))
            message.Headers.TryAddWithoutValidation(StepUpGrant.HeaderName, stepUpGrant);
        return SendAsync<TokenPurchaseResultDto>(message, bearerToken, ct);
    }

    public Task<BillingResult<TokenPurchaseResultDto>> CancelTokenPurchaseAsync(
        string purchaseId, string bearerToken, CancellationToken ct = default) =>
        SendAsync<TokenPurchaseResultDto>(new HttpRequestMessage(HttpMethod.Post,
            $"api/v1/billing/tokens/purchases/{Uri.EscapeDataString(purchaseId)}/cancel"), bearerToken, ct);

    private async Task<BillingResult<T>> SendAsync<T>(HttpRequestMessage request, string bearerToken, CancellationToken ct)
        where T : class
    {
        using (request)
        {
            if (string.IsNullOrWhiteSpace(bearerToken))
                return BillingResult<T>.Fail(StatusCodes.Status401Unauthorized, "AUTH_TOKEN_MISSING", "Caller bearer token is required.");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            var client = _factory.CreateClient(HttpClientName);
            var path   = request.RequestUri?.OriginalString;

            try
            {
                using var response = await client.SendAsync(request, ct);
                var status = (int)response.StatusCode;
                var body   = await TryReadAsync<T>(response, ct);

                if (response.IsSuccessStatusCode && body?.Success == true && body.Data is not null)
                    return BillingResult<T>.Ok(body.Data);

                // 4xx con código: es la respuesta de Billing y se devuelve tal cual. El 401 no: el
                // BizCenter ya aceptó el token, así que es un desacuerdo entre servicios y no una
                // sesión caducada (un 401 sacaría al miembro de la sesión).
                if (status is >= 400 and < 500 && status != StatusCodes.Status401Unauthorized
                    && !string.IsNullOrWhiteSpace(body?.ErrorCode))
                {
                    // ApiResponse.Fail rellena Errors, no Message.
                    var message = body.Errors?.FirstOrDefault() ?? body.Message ?? $"Billing returned {status}.";
                    _logger.LogInformation("Billing respondió {Status} {Code} a {Path}.", status, body.ErrorCode, path);
                    return BillingResult<T>.Fail(status, body.ErrorCode!, message);
                }

                _logger.LogWarning("Respuesta inesperada de Billing: {Status} {Code} a {Path}.", status, body?.ErrorCode, path);
                return BillingResult<T>.Unavailable("Billing returned an unexpected response.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "No se pudo llamar a Billing ({Path}).", path);
                return BillingResult<T>.Unavailable("Billing is unavailable.");
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Billing no respondió a tiempo ({Path}).", path);
                return BillingResult<T>.Unavailable("Billing did not respond in time.");
            }
        }
    }

    private static async Task<ApiResponse<T>?> TryReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        try { return await response.Content.ReadFromJsonAsync<ApiResponse<T>>(cancellationToken: ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return null; }
    }
}
