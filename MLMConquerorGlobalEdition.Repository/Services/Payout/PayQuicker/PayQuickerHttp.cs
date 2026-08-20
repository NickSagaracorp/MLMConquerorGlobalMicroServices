using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Payout.PayQuicker;

/// <summary>
/// Plomería HTTP compartida por los clientes v1 y v2.
///
/// Todo va por <see cref="IHttpClientFactory"/> con headers POR REQUEST. La integración
/// de MWRLife usaba un HttpClient estático y le mutaba DefaultRequestHeaders.Authorization
/// en cada llamada: bajo concurrencia dos pagos en paralelo podían pisarse la credencial
/// y salir con el header del otro. Acá eso es imposible por construcción.
/// </summary>
public static class PayQuickerHttp
{
    public const string ClientName = "payquicker";

    /// <summary>
    /// v2 versiona por fecha y EXIGE el header "api-version" con formato de PUNTOS.
    /// Con guiones, en el query-string, o ausente, el API responde 400 con body vacío.
    /// </summary>
    public const string V2ApiVersion = "2026.02.01";

    public static readonly JsonSerializerOptions Json = new()
    {
        // PayQuicker es una unión discriminada: mandar campos que no aplican al
        // transferType hace que rechace el body. Omitir nulos es obligatorio, no cosmético.
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public static HttpRequestMessage BuildRequest(
        HttpMethod method,
        string url,
        string accessToken,
        string apiVersion,
        object? payload = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        if (apiVersion == "V2")
            request.Headers.TryAddWithoutValidation("api-version", V2ApiVersion);
        else
            request.Headers.TryAddWithoutValidation("X-MyPayQuicker-Version", "01-15-2018");

        if (payload is not null)
        {
            var json = JsonSerializer.Serialize(payload, payload.GetType(), Json);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return request;
    }

    /// <summary>
    /// Código que devuelve ReadAsync cuando el proveedor dice explícitamente que el recurso
    /// NO existe. Es distinto de un fallo: significa "no está", no "no se pudo averiguar",
    /// y quien llama debe poder seguir adelante y crearlo.
    /// </summary>
    public const string NotFoundErrorCode = "PAYQUICKER_NOT_FOUND";

    /// <summary>
    /// Deserializa una respuesta exitosa a <typeparamref name="T"/>, o devuelve un Result
    /// de error. Si el proveedor respondió que el recurso no existe, el código es
    /// <see cref="NotFoundErrorCode"/>.
    /// </summary>
    public static async Task<Result<T>> ReadAsync<T>(
        HttpResponseMessage response,
        string operation,
        CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            // PayQuicker NO usa 404 para "no encontrado": responde 400 con un cuerpo
            // {"severity":"Critical","error":"NoResultFound",...}. Tratarlo como fallo duro
            // haría que un miembro que todavía no tiene cuenta nunca llegue a que se la creen.
            var detail = TryReadError(body);

            if (string.Equals(detail?.Error, "NoResultFound", StringComparison.OrdinalIgnoreCase))
                return Result<T>.Failure(
                    NotFoundErrorCode,
                    detail!.Message ?? $"PayQuicker {operation}: the resource does not exist yet.");

            // Para el resto se prefiere el mensaje del proveedor al volcado crudo del cuerpo.
            return Result<T>.Failure(
                $"PAYQUICKER_HTTP_{(int)response.StatusCode}",
                detail?.Message is { Length: > 0 } m
                    ? $"PayQuicker {operation} failed ({(int)response.StatusCode}): {m}"
                    : $"PayQuicker {operation} failed ({(int)response.StatusCode}): {Truncate(body)}");
        }

        if (string.IsNullOrWhiteSpace(body))
            return Result<T>.Failure(
                "PAYQUICKER_EMPTY_RESPONSE",
                $"PayQuicker {operation} returned an empty body with status {(int)response.StatusCode}.");

        try
        {
            var value = JsonSerializer.Deserialize<T>(body, Json);
            return value is null
                ? Result<T>.Failure("PAYQUICKER_MALFORMED_RESPONSE",
                    $"PayQuicker {operation} returned a body that deserialized to null: {Truncate(body)}")
                : Result<T>.Success(value);
        }
        catch (JsonException ex)
        {
            return Result<T>.Failure(
                "PAYQUICKER_MALFORMED_RESPONSE",
                $"PayQuicker {operation} returned unparseable JSON ({ex.Message}): {Truncate(body)}");
        }
    }

    /// <summary>
    /// Los montos viajan como string. Se fuerza "0.00" con cultura invariante: con
    /// ToString() a secas un decimal 5.020000m sale "5.020000" y una cultura es-ES lo
    /// escribiría "5,02", que PayQuicker rechaza.
    /// </summary>
    public static string FormatAmount(decimal amount) =>
        amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Forma del cuerpo de error de PayQuicker. Null si no se pudo parsear.</summary>
    private static PayQuickerErrorBody? TryReadError(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try { return JsonSerializer.Deserialize<PayQuickerErrorBody>(body, Json); }
        catch (JsonException) { return null; }
    }

    private sealed class PayQuickerErrorBody
    {
        [System.Text.Json.Serialization.JsonPropertyName("severity")]    public string? Severity    { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("error")]       public string? Error       { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("code")]        public int     Code        { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("message")]     public string? Message     { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("referenceId")] public string? ReferenceId { get; set; }
    }

    private static string Truncate(string s, int max = 500) =>
        string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s[..max] + "…");
}
