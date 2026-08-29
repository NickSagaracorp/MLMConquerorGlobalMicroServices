using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminWeb.Services;

/// <summary>
/// La única forma de hablar con SignupAPI desde el portal de administración: monta la petición,
/// le pone el Bearer del usuario cuando hace falta, desenvuelve el <see cref="ApiResponse{T}"/> y
/// convierte cualquier final infeliz —error de la API, 401, 429, red caída, cuerpo que no es
/// JSON— en un código de error que las pantallas ya saben traducir.
///
/// Existe porque diez manejadores de formulario hacían exactamente estas cinco cosas. Copiadas
/// diez veces son diez sitios donde arreglar el mismo fallo, y —lo que es peor— diez sitios donde
/// uno puede olvidarse del <c>try</c> y convertir un microservicio caído en un 500 en la cara del
/// usuario en vez de en un mensaje.
///
/// El token de acceso sale del claim <c>access_token</c> de la cookie de sesión, que es donde lo
/// dejó <c>AuthEndpoints.CompleteSignInAsync</c> — el mismo sitio del que lo saca
/// <see cref="AdminApiAuthHandler"/> para AdminAPI. Aquí no se usa aquel manejador porque el
/// cliente "AuthApi" es deliberadamente anónimo: por él pasan el login y la recuperación de
/// contraseña, que ocurren cuando todavía no hay sesión ninguna.
/// </summary>
public sealed class AuthApiGateway
{
    /// <summary>
    /// Sin token en la cookie no hay nada que mandar. Se devuelve como código de error y no como
    /// excepción porque para el usuario es lo mismo que una sesión caducada, y la salida —volver
    /// al login— también.
    /// </summary>
    public const string SessionExpired = "SESSION_EXPIRED";

    /// <summary>La API no respondió: red, DNS, certificado o el propio servicio caído.</summary>
    public const string Unreachable = "SERVICE_UNAVAILABLE";

    private readonly IHttpClientFactory        _httpClientFactory;
    private readonly IHttpContextAccessor      _httpContextAccessor;
    private readonly ILogger<AuthApiGateway>   _logger;

    public AuthApiGateway(
        IHttpClientFactory      httpClientFactory,
        IHttpContextAccessor    httpContextAccessor,
        ILogger<AuthApiGateway> logger)
    {
        _httpClientFactory   = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _logger              = logger;
    }

    /// <summary>El token de acceso de la sesión, o null si no hay sesión.</summary>
    public string? AccessToken =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue("access_token");

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
        using var request = new HttpRequestMessage(method, path);

        if (body is not null)
            request.Content = JsonContent.Create(body);

        if (authenticated)
        {
            var token = AccessToken;
            if (string.IsNullOrWhiteSpace(token))
                return ApiOutcome<T>.Failed(SessionExpired);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient("AuthApi");
            using var response = await httpClient.SendAsync(request, ct);

            var apiResponse = await ReadEnvelopeAsync<T>(response, ct);

            if (response.IsSuccessStatusCode && apiResponse?.Success == true)
                return ApiOutcome<T>.Succeeded(apiResponse.Data);

            return ApiOutcome<T>.Failed(ErrorCodeOf(response, apiResponse?.ErrorCode));
        }
        catch (Exception ex)
        {
            // Que el servicio de autenticación esté caído no puede tumbar el portal entero: la
            // pantalla que llamó tiene que poder enseñar un mensaje y dejar reintentar.
            _logger.LogError(ex, "La llamada {Method} {Path} a la API de autenticación falló.",
                method.Method, path);
            return ApiOutcome<T>.Failed(Unreachable);
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
