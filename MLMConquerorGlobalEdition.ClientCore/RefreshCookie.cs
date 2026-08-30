namespace MLMConquerorGlobalEdition.ClientCore;

/// <summary>
/// El refresh token de SignupAPI NO viaja por el cuerpo: la API lo vacía a propósito
/// (<c>response.RefreshToken = string.Empty</c>) y lo entrega en una cabecera <c>Set-Cookie</c>.
/// Esta clase es el único sitio donde se sabe eso.
/// </summary>
/// <remarks>
/// POR QUÉ HACE FALTA. En un navegador esto no se programa: la cookie la guarda y la reenvía él
/// solo. Pero los dos portales hablan con SignupAPI SERVIDOR A SERVIDOR, y ahí no hay navegador:
/// para conseguir el refresh token hay que leer la cabecera de la respuesta a mano, y para usarlo
/// hay que volver a mandarlo a mano como cabecera <c>Cookie</c> de la petición.
///
/// NO SE USA EL <c>CookieContainer</c> DE <c>HttpClient</c>, y esto es lo importante de todo el
/// archivo. <c>HttpClientHandler.UseCookies</c> viene en <c>true</c> por defecto, y
/// <c>IHttpClientFactory</c> construye UN manejador primario por cliente con nombre que reutiliza
/// para TODAS las llamadas de TODOS los usuarios. Con las cookies encendidas, el refresh token del
/// usuario que acaba de entrar se guardaría en un contenedor compartido y se enviaría en la
/// siguiente llamada de cualquier otro: una sesión hablando con la credencial de otra. Por eso el
/// cliente <c>AuthApi</c> se registra con <c>UseCookies = false</c>
/// (<c>AuthSurfaceExtensions.AddAuthApiClient</c>) y cada llamada lleva —o no— exactamente el token
/// que le corresponde.
///
/// Vive en ClientCore porque no tiene nada de web: es <c>System.Net.Http</c> y análisis de texto. Una
/// aplicación MAUI que hable con la misma API tiene el mismo problema y la misma solución.
/// </remarks>
public static class RefreshCookie
{
    /// <summary>
    /// El nombre con el que SignupAPI escribe y lee la cookie. Es el contrato entre
    /// <c>AuthController.SetRefreshTokenCookie</c> y <c>AuthController.Refresh</c>, y aquí se
    /// escribe una sola vez para que no se pueda escribir con un nombre y buscar con otro.
    /// </summary>
    public const string Name = "refresh_token";

    /// <summary>Nombre de la cabecera de respuesta que trae la cookie.</summary>
    private const string SetCookieHeader = "Set-Cookie";

    /// <summary>
    /// El refresh token que la API dejó en la respuesta, o null si no dejó ninguno.
    /// </summary>
    /// <remarks>
    /// Una respuesta puede traer VARIAS cabeceras <c>Set-Cookie</c>, así que se recorren todas y se
    /// mira el nombre de cada una. Del resto de la cookie —<c>Path</c>, <c>HttpOnly</c>,
    /// <c>Expires</c>…— no se hace nada: son instrucciones para un navegador, y aquí el que decide
    /// dónde se guarda el token es el portal.
    /// </remarks>
    public static string? ReadFrom(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues(SetCookieHeader, out var cookies))
            return null;

        foreach (var cookie in cookies)
        {
            var token = ValueOf(cookie);
            if (!string.IsNullOrEmpty(token)) return token;
        }

        return null;
    }

    /// <summary>
    /// El valor de la cabecera <c>Cookie</c> con la que se le devuelve el token a la API.
    /// </summary>
    public static string RequestHeader(string token) => $"{Name}={token}";

    /// <summary>
    /// El valor de una cabecera <c>Set-Cookie</c> si es la del refresh token, o null si es otra.
    /// </summary>
    /// <remarks>
    /// El formato es <c>nombre=valor; Atributo; Atributo=…</c>. Solo interesa el primer par, que es
    /// el único que lleva el nombre y el valor de la cookie; todo lo que va detrás del primer
    /// punto y coma son atributos.
    /// </remarks>
    private static string? ValueOf(string? setCookie)
    {
        if (string.IsNullOrWhiteSpace(setCookie)) return null;

        var end  = setCookie.IndexOf(';');
        var pair = end < 0 ? setCookie : setCookie[..end];

        var separator = pair.IndexOf('=');
        if (separator <= 0) return null;

        if (!pair.AsSpan(0, separator).Trim().Equals(Name, StringComparison.Ordinal))
            return null;

        var value = pair.AsSpan(separator + 1).Trim().ToString();
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
