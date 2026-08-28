using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminWeb.Services;

public static class AuthEndpoints
{
    /// <summary>
    /// Cookie que guarda el ChallengeToken del segundo factor entre el POST de login y el POST
    /// del código. Va en cookie y no en la URL: en la URL quedaría en el historial del navegador,
    /// en los registros del proxy y en la cabecera Referer de cualquier recurso externo que
    /// cargue la página.
    /// </summary>
    /// <remarks>
    /// <c>internal</c> y no <c>private</c> porque <see cref="TwoFactorPageData"/> lee estas dos
    /// cookies para pintar las páginas. Un segundo literal con el mismo nombre en otro archivo
    /// es la clase de duplicado que se desincroniza en silencio.
    /// </remarks>
    internal const string ChallengeCookie = "mlm_admin_2fa_challenge";

    /// <summary>
    /// Cookie del EnrollmentToken. Deliberadamente distinta de <see cref="ChallengeCookie"/>:
    /// son propósitos distintos y compartir nombre invita a redimir uno donde va el otro.
    /// </summary>
    internal const string EnrollmentCookie = "mlm_admin_2fa_enrollment";

    /// <summary>Ventana de vida de ambas cookies. El reto de la API dura menos; esto es el techo.</summary>
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(10);

    private static readonly string[] AdminRoles =
    {
        "SuperAdmin", "Admin", "CommissionManager",
        "BillingManager", "SupportManager",
        "SupportLevel1", "SupportLevel2", "SupportLevel3", "IT"
    };

    /// <summary>
    /// Handles the HTML form POST from Login.razor.
    /// Validates credentials against SignupAPI and either signs the user in, o lo desvía al
    /// segundo factor / al enrolamiento cuando la API lo pide.
    /// </summary>
    public static async Task<IResult> LoginAsync(
        [Microsoft.AspNetCore.Mvc.FromForm] LoginRequest request,
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        CancellationToken ct)
    {
        // Auth lives in the SignupAPI — runs server-side so cookie goes to the real browser request
        var httpClient = httpClientFactory.CreateClient("AuthApi");
        var response = await httpClient.PostAsJsonAsync("api/v1/auth/login",
            new { request.Email, request.Password }, ct);

        if (!response.IsSuccessStatusCode)
            return Results.Redirect("/admin/login?error=invalid");

        var apiResponse = await ReadAuthResponseAsync(response, ct);
        if (apiResponse?.Success != true || apiResponse.Data is null)
            return Results.Redirect("/admin/login?error=invalid");

        // Ramificar ANTES de tocar el token: en estas dos ramas AccessToken viene vacío y leerlo
        // hacía fallar CanReadToken, que devolvía "credenciales inválidas" con credenciales buenas.
        if (apiResponse.Data.RequiresEnrollment)
        {
            if (string.IsNullOrWhiteSpace(apiResponse.Data.EnrollmentToken))
                return Results.Redirect("/admin/login?error=invalid");

            SetChallengeCookie(httpContext, EnrollmentCookie, apiResponse.Data.EnrollmentToken!);
            return Results.Redirect("/admin/enroll-authenticator");
        }

        if (apiResponse.Data.RequiresTwoFactor)
        {
            if (string.IsNullOrWhiteSpace(apiResponse.Data.ChallengeToken))
                return Results.Redirect("/admin/login?error=invalid");

            SetChallengeCookie(httpContext, ChallengeCookie, apiResponse.Data.ChallengeToken!);
            return Results.Redirect($"/admin/login-2fa{TargetQuery(apiResponse.Data.MaskedTarget, '?')}");
        }

        return await CompleteSignInAsync(httpContext, apiResponse.Data.AccessToken, "/admin/login");
    }

    /// <summary>
    /// Segundo paso del login: canjea el código de 6 dígitos junto con el ChallengeToken que
    /// viaja en cookie. El código llega del formulario; el reto nunca sale de la cookie.
    /// </summary>
    public static async Task<IResult> LoginTwoFactorAsync(
        [Microsoft.AspNetCore.Mvc.FromForm] TwoFactorCodeRequest request,
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var challengeToken = httpContext.Request.Cookies[ChallengeCookie];
        if (string.IsNullOrWhiteSpace(challengeToken))
            return SessionExpired(httpContext);

        var httpClient = httpClientFactory.CreateClient("AuthApi");
        var response = await httpClient.PostAsJsonAsync("api/v1/auth/two-factor/verify",
            new { ChallengeToken = challengeToken, Code = request.Code ?? string.Empty }, ct);

        var apiResponse = await ReadAuthResponseAsync(response, ct);

        if (!response.IsSuccessStatusCode || apiResponse?.Success != true || apiResponse.Data is null)
        {
            // Un reto inválido o caducado ya no sirve para nada: fuera la cookie.
            var code = ErrorCodeOf(response, apiResponse, "CODE_INVALID");
            if (code is "INVALID_CHALLENGE" or "CODE_EXPIRED" or "TOO_MANY_ATTEMPTS")
                DeleteChallengeCookie(httpContext, ChallengeCookie);

            return Results.Redirect($"/admin/login-2fa?error={Uri.EscapeDataString(code)}");
        }

        DeleteChallengeCookie(httpContext, ChallengeCookie);
        return await CompleteSignInAsync(httpContext, apiResponse.Data.AccessToken, "/admin/login");
    }

    /// <summary>
    /// Reenvía el código. La API emite un reto nuevo, así que la cookie se refresca con él;
    /// si no devuelve ninguno, la anterior sigue siendo la válida y se deja como está.
    /// </summary>
    public static async Task<IResult> ResendTwoFactorAsync(
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var challengeToken = httpContext.Request.Cookies[ChallengeCookie];
        if (string.IsNullOrWhiteSpace(challengeToken))
            return SessionExpired(httpContext);

        var httpClient = httpClientFactory.CreateClient("AuthApi");
        var response = await httpClient.PostAsJsonAsync("api/v1/auth/two-factor/resend",
            new { ChallengeToken = challengeToken }, ct);

        var apiResponse = await ReadAuthResponseAsync(response, ct);

        if (!response.IsSuccessStatusCode || apiResponse?.Success != true || apiResponse.Data is null)
        {
            var code = ErrorCodeOf(response, apiResponse, "CHANNEL_UNAVAILABLE");
            if (code == "INVALID_CHALLENGE")
                DeleteChallengeCookie(httpContext, ChallengeCookie);

            return Results.Redirect($"/admin/login-2fa?error={Uri.EscapeDataString(code)}");
        }

        if (!string.IsNullOrWhiteSpace(apiResponse.Data.ChallengeToken))
            SetChallengeCookie(httpContext, ChallengeCookie, apiResponse.Data.ChallengeToken!);

        return Results.Redirect(
            $"/admin/login-2fa?resent=1{TargetQuery(apiResponse.Data.MaskedTarget, '&')}");
    }

    /// <summary>
    /// Cierra el enrolamiento con el primer código de la aplicación autenticadora y deja al
    /// usuario dentro: la API devuelve tokens reales al confirmar, no hace falta reloguear.
    /// </summary>
    public static async Task<IResult> EnrollAuthenticatorAsync(
        [Microsoft.AspNetCore.Mvc.FromForm] TwoFactorCodeRequest request,
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var enrollmentToken = httpContext.Request.Cookies[EnrollmentCookie];
        if (string.IsNullOrWhiteSpace(enrollmentToken))
            return SessionExpired(httpContext);

        var httpClient = httpClientFactory.CreateClient("AuthApi");
        var response = await httpClient.PostAsJsonAsync("api/v1/auth/two-factor/enroll/confirm",
            new { EnrollmentToken = enrollmentToken, Code = request.Code ?? string.Empty }, ct);

        var apiResponse = await ReadAuthResponseAsync(response, ct);

        if (!response.IsSuccessStatusCode || apiResponse?.Success != true || apiResponse.Data is null)
        {
            var code = ErrorCodeOf(response, apiResponse, "CODE_INVALID");
            if (code is "INVALID_CHALLENGE" or "CODE_EXPIRED" or "TOO_MANY_ATTEMPTS")
                DeleteChallengeCookie(httpContext, EnrollmentCookie);

            return Results.Redirect($"/admin/enroll-authenticator?error={Uri.EscapeDataString(code)}");
        }

        DeleteChallengeCookie(httpContext, EnrollmentCookie);
        return await CompleteSignInAsync(httpContext, apiResponse.Data.AccessToken, "/admin/login");
    }

    public static async Task<IResult> LogoutAsync(HttpContext httpContext)
    {
        DeleteChallengeCookie(httpContext, ChallengeCookie);
        DeleteChallengeCookie(httpContext, EnrollmentCookie);
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/admin/login");
    }

    /// <summary>
    /// Único punto donde se construye el ClaimsPrincipal y se firma la sesión. Las tres rutas de
    /// entrada (login directo, verificación del segundo factor y confirmación del enrolamiento)
    /// pasan por aquí, así que la comprobación de rol admin se aplica siempre sobre el token
    /// final y nunca sobre un reto.
    /// </summary>
    private static async Task<IResult> CompleteSignInAsync(
        HttpContext httpContext,
        string? accessToken,
        string failureRedirect)
    {
        // Parse the JWT to extract claims for the cookie identity
        var handler = new JwtSecurityTokenHandler();
        if (string.IsNullOrWhiteSpace(accessToken) || !handler.CanReadToken(accessToken))
            return Results.Redirect($"{failureRedirect}?error=invalid");

        var jwt    = handler.ReadJwtToken(accessToken);
        var claims = jwt.Claims.ToList();
        claims.Add(new Claim("access_token", accessToken!));

        // Ensure the user has at least one admin-level role
        var roles = claims
            .Where(c => c.Type == ClaimTypes.Role ||
                        c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
            .Select(c => c.Value)
            .ToList();

        if (!roles.Any(r => AdminRoles.Contains(r)))
            return Results.Redirect($"{failureRedirect}?error=access_denied");

        // Build ClaimsPrincipal and sign in — httpContext here IS the browser's real request
        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });

        return Results.Redirect("/admin");
    }

    /// <summary>
    /// Fragmento <c>target=…</c> para la pantalla del segundo factor, o cadena vacía si la API no
    /// devolvió destino (el autenticador no envía nada a ninguna parte).
    /// </summary>
    /// <remarks>
    /// Aquí sí va en la URL, al revés que el ChallengeToken: lo que se pasa ya viene enmascarado
    /// por la API (<c>n****@@dominio.com</c>, <c>***4321</c>) y no es una credencial. Sin esto, un
    /// usuario de SMS no tendría manera de ver a qué número se envió el código: el teléfono no
    /// viaja en el reto y la página no tiene otra fuente.
    /// </remarks>
    private static string TargetQuery(string? maskedTarget, char separator) =>
        string.IsNullOrWhiteSpace(maskedTarget)
            ? string.Empty
            : $"{separator}target={Uri.EscapeDataString(maskedTarget)}";

    /// <summary>Mismas opciones que la cookie de sesión (<c>mlm_admin_cookie</c>), con vida corta.</summary>
    private static void SetChallengeCookie(HttpContext httpContext, string name, string value) =>
        httpContext.Response.Cookies.Append(name, value, new CookieOptions
        {
            HttpOnly    = true,
            Secure      = true,
            SameSite    = SameSiteMode.Strict,
            IsEssential = true,
            Path        = "/",
            Expires     = DateTimeOffset.UtcNow.Add(ChallengeLifetime)
        });

    private static void DeleteChallengeCookie(HttpContext httpContext, string name) =>
        httpContext.Response.Cookies.Delete(name, new CookieOptions
        {
            HttpOnly = true,
            Secure   = true,
            SameSite = SameSiteMode.Strict,
            Path     = "/"
        });

    /// <summary>
    /// Sin cookie no hay nada que canjear: caducó o alguien entró directo a la URL. Vuelta al
    /// login con un código que esa página ya sabe mostrar.
    /// </summary>
    private static IResult SessionExpired(HttpContext httpContext)
    {
        DeleteChallengeCookie(httpContext, ChallengeCookie);
        DeleteChallengeCookie(httpContext, EnrollmentCookie);
        return Results.Redirect("/admin/login?error=session_expired");
    }

    private static async Task<ApiResponse<AuthTokens>?> ReadAuthResponseAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ApiResponse<AuthTokens>>(cancellationToken: ct);
        }
        catch
        {
            // Cuerpo no-JSON (p. ej. el 429 que emite el limitador de tasa antes del pipeline MVC).
            return null;
        }
    }

    /// <summary>
    /// Propaga el CÓDIGO de la API, no su mensaje: el texto que ve el usuario lo decide la
    /// interfaz, que es la que puede traducirlo.
    /// </summary>
    private static string ErrorCodeOf(
        HttpResponseMessage response, ApiResponse<AuthTokens>? apiResponse, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(apiResponse?.ErrorCode))
            return apiResponse.ErrorCode!;

        return response.StatusCode == HttpStatusCode.TooManyRequests
            ? "TOO_MANY_REQUESTS"
            : fallback;
    }

    public record LoginRequest(string Email, string Password);

    /// <summary>El formulario solo aporta el código; el reto vive en la cookie.</summary>
    public record TwoFactorCodeRequest(string? Code);

    private sealed record AuthTokens
    {
        public string  AccessToken        { get; init; } = string.Empty;
        public string  RefreshToken       { get; init; } = string.Empty;
        public DateTime TokenExpiry       { get; init; }
        public bool    RequiresTwoFactor  { get; init; }
        public string? ChallengeToken     { get; init; }
        public bool    RequiresEnrollment { get; init; }
        public string? EnrollmentToken    { get; init; }
        public string? Channel            { get; init; }
        public string? MaskedTarget       { get; init; }
    }
}
