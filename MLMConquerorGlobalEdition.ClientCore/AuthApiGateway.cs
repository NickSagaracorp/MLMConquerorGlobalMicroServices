using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.ClientCore;

/// <summary>
/// La única forma de hablar con SignupAPI desde un cliente: monta la petición, le pone el Bearer
/// del usuario cuando hace falta, desenvuelve el <see cref="ApiResponse{T}"/> y convierte cualquier
/// final infeliz —error de la API, 401, 429, red caída, cuerpo que no es JSON— en un código de
/// error que las pantallas ya saben traducir.
///
/// Existe porque diez manejadores de formulario hacían exactamente estas cinco cosas. Copiadas
/// diez veces son diez sitios donde arreglar el mismo fallo, y —lo que es peor— diez sitios donde
/// uno puede olvidarse del <c>try</c> y convertir un microservicio caído en un 500 en la cara del
/// usuario en vez de en un mensaje.
///
/// Vive en ClientCore y no en SharedComponents porque nada de esto es de un portal web: el nombre
/// del cliente HTTP, las rutas de la API y los códigos de error son los mismos en administración,
/// en el centro de negocios y en la aplicación móvil que vendrá. De dónde sale el token era lo
/// único que sí cambiaba, y por eso entra por <see cref="IAccessTokenProvider"/> en vez de leerse
/// del <c>HttpContext</c>: web lo saca del claim <c>access_token</c> de la cookie de sesión —donde
/// lo dejó <c>AuthEndpoints.CompleteSignInAsync</c>—; móvil, del almacenamiento seguro del
/// dispositivo.
///
/// El cliente "AuthApi" es deliberadamente anónimo —sin manejador de mensajes que autentique—
/// porque por él pasan el login y la recuperación de contraseña, que ocurren cuando todavía no hay
/// sesión ninguna. El Bearer lo pone este gateway, llamada a llamada, solo cuando se le pide.
/// </summary>
public sealed class AuthApiGateway
{
    /// <summary>
    /// Nombre del cliente HTTP con nombre que resuelve a SignupAPI. Su dirección base la configura
    /// cada anfitrión —cambia entre portales y entornos—, pero el nombre no: es el contrato entre
    /// quien registra el cliente y quien lo pide, y por eso está aquí y no repetido en cadenas
    /// sueltas.
    /// </summary>
    public const string HttpClientName = "AuthApi";

    /// <summary>
    /// Sin token no hay nada que mandar. Se devuelve como código de error y no como excepción
    /// porque para el usuario es lo mismo que una sesión caducada, y la salida —volver al login—
    /// también.
    /// </summary>
    public const string SessionExpired = "SESSION_EXPIRED";

    /// <summary>La API no respondió: red, DNS, certificado o el propio servicio caído.</summary>
    public const string Unreachable = "SERVICE_UNAVAILABLE";

    private readonly IHttpClientFactory        _httpClientFactory;
    private readonly IAccessTokenProvider      _accessTokens;
    private readonly ILogger<AuthApiGateway>   _logger;

    public AuthApiGateway(
        IHttpClientFactory      httpClientFactory,
        IAccessTokenProvider    accessTokens,
        ILogger<AuthApiGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _accessTokens      = accessTokens;
        _logger            = logger;
    }

    /// <summary>El token de acceso de la sesión, o null si no hay sesión.</summary>
    /// <remarks>
    /// Público porque hay una llamada que este gateway no puede envolver: la descarga de datos
    /// personales devuelve un archivo, no un <see cref="ApiResponse{T}"/>, así que quien la sirve
    /// necesita el token en crudo para montarse su propia petición.
    /// </remarks>
    public ValueTask<string?> GetAccessTokenAsync(CancellationToken ct = default) =>
        _accessTokens.GetAccessTokenAsync(ct);

    /// <summary>
    /// Llama a la API y devuelve el <c>Data</c> del sobre, o el código del error.
    /// </summary>
    /// <param name="authenticated">
    /// Si va con el Bearer de la sesión. Los flujos previos a tener sesión —recuperar contraseña,
    /// confirmar correo— van en false; la gestión de la cuenta, en true.
    /// </param>
    public async Task<ApiOutcome<T>> CallAsync<T>(
        HttpMethod        method,
        string            path,
        object?           body,
        bool              authenticated,
        CancellationToken ct = default)
    {
        var (outcome, _) = await SendAsync<T>(method, path, body, authenticated, ct);
        return outcome;
    }

    /// <summary>
    /// Igual que <see cref="CallAsync{T}"/> para los endpoints que EMITEN TOKENS —login,
    /// verificación del segundo factor, confirmación del enrolamiento—, con el refresh token que la
    /// API deja en la cabecera <c>Set-Cookie</c> de la respuesta.
    /// </summary>
    /// <remarks>
    /// Existe porque el refresh token NO viene en el cuerpo: la API lo vacía a propósito
    /// (<c>response.RefreshToken = string.Empty</c>) y lo entrega como cookie, contando con que al
    /// otro lado hay un navegador. Los portales hablan servidor a servidor, así que si nadie lee esa
    /// cabecera el token se pierde — que es exactamente lo que pasaba, y por lo que la sesión moría
    /// al caducar el JWT.
    ///
    /// Siempre anónima: los tres endpoints que emiten tokens lo son por definición, porque ocurren
    /// cuando todavía no hay sesión.
    /// </remarks>
    public Task<(ApiOutcome<T> Outcome, string? RefreshToken)> CallForTokensAsync<T>(
        HttpMethod        method,
        string            path,
        object?           body,
        CancellationToken ct = default) =>
        SendAsync<T>(method, path, body, authenticated: false, ct);

    /// <summary>
    /// El cuerpo común de las dos formas de llamar. Devuelve además el refresh token de la
    /// respuesta, que casi todas las llamadas descartan y las tres de la puerta necesitan.
    /// </summary>
    private async Task<(ApiOutcome<T> Outcome, string? RefreshToken)> SendAsync<T>(
        HttpMethod        method,
        string            path,
        object?           body,
        bool              authenticated,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);

        if (body is not null)
            request.Content = JsonContent.Create(body);

        if (authenticated)
        {
            var token = await _accessTokens.GetAccessTokenAsync(ct);
            if (string.IsNullOrWhiteSpace(token))
                return (ApiOutcome<T>.Failed(SessionExpired), null);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await httpClient.SendAsync(request, ct);

            var apiResponse  = await ReadEnvelopeAsync<T>(response, ct);
            var refreshToken = RefreshCookie.ReadFrom(response);

            if (response.IsSuccessStatusCode && apiResponse?.Success == true)
                return (ApiOutcome<T>.Succeeded(apiResponse.Data), refreshToken);

            return (ApiOutcome<T>.Failed(ErrorCodeOf(response, apiResponse?.ErrorCode)), null);
        }
        catch (Exception ex)
        {
            // Que el servicio de autenticación esté caído no puede tumbar la aplicación entera: la
            // pantalla que llamó tiene que poder enseñar un mensaje y dejar reintentar.
            _logger.LogError(ex, "La llamada {Method} {Path} a la API de autenticación falló.",
                method.Method, path);
            return (ApiOutcome<T>.Failed(Unreachable), null);
        }
    }

    /// <summary>Igual que <see cref="CallAsync{T}"/> cuando la respuesta no trae datos útiles.</summary>
    public async Task<ApiOutcome> CallAsync(
        HttpMethod        method,
        string            path,
        object?           body,
        bool              authenticated,
        CancellationToken ct = default)
    {
        var outcome = await CallAsync<bool>(method, path, body, authenticated, ct);
        return new ApiOutcome(outcome.Success, outcome.ErrorCode);
    }

    /// <summary>
    /// Propaga el CÓDIGO de la API, no su mensaje: el texto que ve el usuario lo decide la
    /// interfaz, que es la que puede traducirlo. Cuando la API no manda código, el estado HTTP da
    /// uno razonable; si tampoco, se devuelve null y quien llama pone el suyo.
    /// </summary>
    private static string? ErrorCodeOf(HttpResponseMessage response, string? apiErrorCode)
    {
        if (!string.IsNullOrWhiteSpace(apiErrorCode))
            return apiErrorCode;

        return response.StatusCode switch
        {
            HttpStatusCode.TooManyRequests => "TOO_MANY_REQUESTS",
            HttpStatusCode.Unauthorized    => SessionExpired,
            _                              => null
        };
    }

    private async Task<ApiResponse<T>?> ReadEnvelopeAsync<T>(
        HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ApiResponse<T>>(cancellationToken: ct);
        }
        catch
        {
            // Cuerpo no-JSON (p. ej. el 429 que emite el limitador de tasa antes del pipeline
            // MVC, o una página de error de un proxy intermedio).
            return null;
        }
    }
}

/// <summary>Resultado de una llamada que no devuelve datos.</summary>
/// <param name="ErrorCode">Código de la API, o null si el fallo no traía ninguno.</param>
public readonly record struct ApiOutcome(bool Success, string? ErrorCode)
{
    /// <summary>El código a poner en la URL de vuelta, con el respaldo de quien llama.</summary>
    public string ErrorCodeOr(string fallback) =>
        string.IsNullOrWhiteSpace(ErrorCode) ? fallback : ErrorCode!;
}

/// <inheritdoc cref="ApiOutcome"/>
public readonly record struct ApiOutcome<T>(bool Success, string? ErrorCode, T? Data)
{
    public static ApiOutcome<T> Succeeded(T? data) => new(true, null, data);
    public static ApiOutcome<T> Failed(string? errorCode) => new(false, errorCode, default);

    /// <inheritdoc cref="ApiOutcome.ErrorCodeOr"/>
    public string ErrorCodeOr(string fallback) =>
        string.IsNullOrWhiteSpace(ErrorCode) ? fallback : ErrorCode!;
}
