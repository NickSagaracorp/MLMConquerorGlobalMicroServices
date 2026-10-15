using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.BizCenter.Services;

/// <inheritdoc cref="IBillingClient"/>
/// <remarks>
/// SIN REINTENTOS AUTOMÁTICOS: si la compra se corta, reintenta la persona con la MISMA clave de
/// idempotencia y Billing devuelve la compra que ya hizo.
///
/// CÓMO SE CLASIFICA LO QUE CONTESTA BILLING (la pantalla decide con el código si conserva la clave):
///   • 2xx con datos: la respuesta.
///   • 4xx con su código (400 CARD_PAYMENTS_DISABLED, 409 IDEMPOTENCY_KEY_REUSED, 403 STEP_UP_REQUIRED…):
///     respuesta definitiva sobre esa clave; se devuelve tal cual, estado y código.
///   • 401: 502 BILLING_AUTH_MISMATCH. El BizCenter ya aceptó el token, así que es un desacuerdo de
///     configuración entre servicios, no una sesión caducada: un 401 sacaría al miembro de la sesión.
///   • 4xx sin código (ProblemDetails, 404 de ruta, 405, 415, cuerpo ilegible): 502
///     BILLING_CONTRACT_ERROR. Billing no ejecutó nada, pero no nos entendemos; reintentar no lo arregla.
///   • Lo demás (no contestó, se agotó el tiempo, un 5xx, un 2xx ilegible): 502 BILLING_UNAVAILABLE. No
///     se sabe si la compra llegó a hacerse y la pantalla reintenta con la misma clave, nunca con otra.
///
/// EL SOBRE SE LEE SIN EL TIPO DE LOS DATOS. Un error de Billing puede traer en "data" algo que no es
/// T (el Forbidden mandaba "data":false); leer ApiResponse&lt;T&gt; directamente lanzaba y un 403 con
/// código acababa en 502. Primero el sobre, y "data" solo se convierte a T cuando es un éxito.
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

                if (response.IsSuccessStatusCode && body is { Success: true, Data: not null })
                    return BillingResult<T>.Ok(body.Data);

                if (status == StatusCodes.Status401Unauthorized)
                {
                    _logger.LogError(
                        "Billing rechazó con 401 un token que el BizCenter aceptó ({Path}, código {Code}). Revisa que llave, emisor y audiencia coincidan.",
                        path, body?.ErrorCode);
                    return BillingResult<T>.AuthMismatch();
                }

                if (status is >= 400 and < 500)
                {
                    if (!string.IsNullOrWhiteSpace(body?.ErrorCode))
                    {
                        _logger.LogInformation("Billing respondió {Status} {Code} a {Path}.", status, body.ErrorCode, path);
                        return BillingResult<T>.Fail(status, body.ErrorCode!, body.Message ?? $"Billing returned {status}.");
                    }

                    _logger.LogError(
                        "Billing respondió {Status} sin código de error a {Path}: el BizCenter y Billing no comparten contrato (ruta, método, tipo de contenido o validación).",
                        status, path);
                    return BillingResult<T>.ContractError();
                }

                _logger.LogWarning("Respuesta inesperada de Billing: {Status} {Code} a {Path}.", status, body?.ErrorCode, path);
                return BillingResult<T>.Unavailable("Billing returned an unexpected response.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "No se pudo llamar a Billing ({Path}).", path);
                return BillingResult<T>.Unavailable("Billing is unavailable.");
            }
            // Tiempo agotado (o cualquier cancelación que no pidió quien llama): no se sabe si Billing
            // hizo la compra, así que es BILLING_UNAVAILABLE y la pantalla repite con la misma clave.
            // Si la cancelación es de quien llama (el navegador se fue), sube tal cual: no es un
            // resultado, ni éxito ni fallo definitivo, y la pantalla conserva su intento.
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Billing no respondió a tiempo ({Path}).", path);
                return BillingResult<T>.Unavailable("Billing did not respond in time.");
            }
        }
    }

    /// <summary>Lo que importa del sobre de Billing, con "data" ya convertida a T si era un éxito.</summary>
    private sealed record Envelope<T>(bool Success, T? Data, string? ErrorCode, string? Message);

    /// <summary>
    /// Lee el sobre como <c>ApiResponse&lt;JsonElement&gt;</c>: así un "data" de otro tipo (false, null, un
    /// objeto de otra forma) no impide leer el código. El mensaje sale de Errors (lo que rellena
    /// ApiResponse.Fail) o, si no hay, de Message. Null si el cuerpo no es un sobre.
    /// </summary>
    private static async Task<Envelope<T>?> TryReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
        where T : class
    {
        ApiResponse<JsonElement>? raw;
        try { raw = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(JsonOptions, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return null; }
        if (raw is null) return null;

        T? data = null;
        if (raw.Success && raw.Data.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null))
        {
            try { data = raw.Data.Deserialize<T>(JsonOptions); }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException) { data = null; }
        }

        var message = raw.Errors?.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e)) ?? raw.Message;
        return new Envelope<T>(raw.Success, data, raw.ErrorCode, message);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
