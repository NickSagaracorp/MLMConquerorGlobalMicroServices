using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.ClientCore;

/// <summary>
/// El par de tokens con el que sigue viva una sesión. El de acceso caduca en minutos; el de
/// refresco es la credencial de larga vida con la que se consigue el siguiente.
/// </summary>
/// <remarks>
/// Los dos van SIEMPRE juntos y nunca por separado, porque la API ROTA el refresh token en cada
/// renovación: quedarse con el nuevo de acceso y con el viejo de refresco es exactamente el fallo
/// que hace caer al usuario al login a mitad de sesión, y con un solo tipo no se puede escribir.
/// </remarks>
public sealed record SessionTokens(string AccessToken, string RefreshToken);

/// <summary>
/// Quien cambia un refresh token por una pareja nueva contra <c>POST /api/v1/auth/refresh</c>.
/// </summary>
/// <remarks>
/// ES LA ÚNICA PIEZA QUE SABE CÓMO SE HABLA CON ESE ENDPOINT, y ese endpoint es raro en dos cosas:
///
///   • el token de entrada NO va en el cuerpo sino en la cabecera <c>Cookie</c>, porque la API lo
///     lee de <c>Request.Cookies["refresh_token"]</c>;
///   • el token de salida tampoco va en el cuerpo —la API lo vacía a propósito— sino en la
///     cabecera <c>Set-Cookie</c> de la respuesta.
///
/// Las dos mitades de esa rareza viven en <see cref="RefreshCookie"/>.
///
/// SIN NINGÚN SERVICIO DE ÁMBITO. Solo pide <see cref="IHttpClientFactory"/>, que es singleton, y por
/// eso esta clase puede registrarse como singleton y ser llamada desde donde haga falta: desde un
/// middleware, desde un <c>DelegatingHandler</c> que la fábrica construyó en su propio ámbito, o
/// desde dentro de un circuito de Blazor. Si pidiera el <c>HttpContext</c> o el proveedor de estado
/// del circuito no podría hacerlo, y renovar es justo algo que hay que poder hacer desde los tres
/// sitios.
///
/// USA EL CLIENTE <c>AuthApi</c>, que es el ANÓNIMO: no lleva enganchado el manejador que pone el
/// Bearer. Es importante que siga siendo así — renovar con el token caducado que se está intentando
/// renovar sería una recursión, y el endpoint es <c>[AllowAnonymous]</c> precisamente porque la
/// credencial que lo autoriza es el refresh token, no el de acceso.
///
/// CUALQUIER FINAL INFELIZ ES <c>null</c>, y quien llama lo trata como "esta sesión está muerta":
/// refresh caducado, revocado, ausente, la API caída o una respuesta que no se entiende. No se
/// distinguen a propósito — para el usuario los cinco terminan igual, en el login, y devolver algo
/// más rico solo invitaría a alguien a reintentar con un token que la API ya rotó.
/// </remarks>
public sealed class AuthTokenRefresher
{
    /// <summary>La ruta de la renovación en SignupAPI.</summary>
    public const string RefreshPath = "api/v1/auth/refresh";

    private readonly IHttpClientFactory            _httpClientFactory;
    private readonly ILogger<AuthTokenRefresher>   _logger;

    public AuthTokenRefresher(
        IHttpClientFactory httpClientFactory, ILogger<AuthTokenRefresher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger            = logger;
    }

    /// <summary>
    /// Cambia <paramref name="refreshToken"/> por la pareja siguiente, o devuelve null si la
    /// sesión ya no se puede renovar.
    /// </summary>
    /// <remarks>
    /// El token que entra QUEDA GASTADO en cuanto esta llamada tiene éxito: la API lo rota. Quien
    /// llame tiene que quedarse con el que sale y no volver a usar el que entró — de eso se encarga
    /// el almacén de sesión del portal, que serializa las renovaciones de una misma sesión para que
    /// no haya dos llamadas aquí con el mismo token.
    /// </remarks>
    public async Task<SessionTokens?> RefreshAsync(
        string? refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, RefreshPath);
            request.Headers.Add("Cookie", RefreshCookie.RequestHeader(refreshToken!));

            var httpClient = _httpClientFactory.CreateClient(AuthApiGateway.HttpClientName);
            using var response = await httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "La renovación del token devolvió {Estado}: la sesión se da por muerta.",
                    (int)response.StatusCode);
                return null;
            }

            var envelope = await response.Content
                .ReadFromJsonAsync<ApiResponse<RefreshedTokens>>(cancellationToken: ct);

            var accessToken = envelope?.Success == true ? envelope.Data?.AccessToken : null;

            // El de refresco NO está en el cuerpo: la API lo vacía y lo manda por Set-Cookie.
            var rotated = RefreshCookie.ReadFrom(response);

            if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(rotated))
            {
                // Si falta cualquiera de los dos, quedarse con el otro es peor que no renovar: se
                // seguiría con un refresh token que la API acaba de invalidar.
                _logger.LogWarning(
                    "La renovación respondió 200 pero sin la pareja completa de tokens " +
                    "(acceso: {HayAcceso}, refresco: {HayRefresco}).",
                    !string.IsNullOrWhiteSpace(accessToken), !string.IsNullOrWhiteSpace(rotated));
                return null;
            }

            return new SessionTokens(accessToken!, rotated!);
        }
        catch (Exception ex)
        {
            // Que SignupAPI no responda no puede tumbar el portal: quien llama manda al usuario al
            // login, que es lo mismo que le pasaba antes de que existiera la renovación.
            _logger.LogError(ex, "La renovación del token de sesión falló.");
            return null;
        }
    }

    /// <summary>
    /// La respuesta de la renovación, recortada a lo único que se usa. El campo
    /// <c>refreshToken</c> del cuerpo existe, pero la API lo manda SIEMPRE vacío; leerlo de aquí
    /// sería quedarse sin token de refresco y no darse cuenta hasta el segundo refresco.
    /// </summary>
    private sealed record RefreshedTokens
    {
        public string AccessToken { get; init; } = string.Empty;
    }
}
