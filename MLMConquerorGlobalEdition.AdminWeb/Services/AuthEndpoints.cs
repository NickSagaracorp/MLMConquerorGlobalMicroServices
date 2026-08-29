using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MLMConquerorGlobalEdition.SharedComponents.Services;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminWeb.Services;

/// <summary>
/// Lo de ENTRAR al portal de administración: login, segundo factor, enrolamiento y salida. Todo
/// ocurre cuando todavía no hay sesión y termina firmando una.
/// </summary>
/// <remarks>
/// Lo de GESTIONAR la cuenta ya con sesión —contraseña, teléfono, datos personales— vive en
/// <c>AccountEndpoints</c>, ya en SharedComponents. Son dos momentos distintos y con los diez
/// manejadores de aquello aquí dentro este archivo pasaba de trescientas líneas a más de
/// seiscientas.
///
/// Esto se queda en el portal —no se compartió con aquello— porque decide quién puede ENTRAR: la
/// lista <see cref="AdminRoles"/> y los destinos <c>/admin/…</c> son de administración, y el centro
/// de negocios admite a otra gente y la manda a otro sitio.
///
/// Las cookies de los retos y sus opciones están en <see cref="ChallengeCookies"/>, ya compartida,
/// que también usa el alta de teléfono. Sus NOMBRES llegan inyectados en
/// <see cref="ChallengeCookieNames"/>: los pone <c>Program.cs</c> de este portal y son los mismos
/// que lee la superficie de cuenta compartida, que es justo lo que impide escribir un reto con un
/// nombre y buscarlo con otro.
/// </remarks>
public static class AuthEndpoints
{
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
        [Microsoft.AspNetCore.Mvc.FromServices] ChallengeCookieNames challengeCookies,
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

            ChallengeCookies.Set(httpContext, challengeCookies.Enrollment, apiResponse.Data.EnrollmentToken!);
            return Results.Redirect("/admin/enroll-authenticator");
        }

        if (apiResponse.Data.RequiresTwoFactor)
        {
            if (string.IsNullOrWhiteSpace(apiResponse.Data.ChallengeToken))
                return Results.Redirect("/admin/login?error=invalid");

            ChallengeCookies.Set(httpContext, challengeCookies.Login, apiResponse.Data.ChallengeToken!);
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
        [Microsoft.AspNetCore.Mvc.FromServices] ChallengeCookieNames challengeCookies,
        CancellationToken ct)
    {
        var challengeToken = ChallengeCookies.Read(httpContext, challengeCookies.Login);
        if (string.IsNullOrWhiteSpace(challengeToken))
            return SessionExpired(httpContext, challengeCookies);

        var httpClient = httpClientFactory.CreateClient("AuthApi");
        var response = await httpClient.PostAsJsonAsync("api/v1/auth/two-factor/verify",
            new { ChallengeToken = challengeToken, Code = request.Code ?? string.Empty }, ct);

        var apiResponse = await ReadAuthResponseAsync(response, ct);

        if (!response.IsSuccessStatusCode || apiResponse?.Success != true || apiResponse.Data is null)
        {
            // Un reto inválido o caducado ya no sirve para nada: fuera la cookie.
            var code = ErrorCodeOf(response, apiResponse, "CODE_INVALID");
            if (code is "INVALID_CHALLENGE" or "CODE_EXPIRED" or "TOO_MANY_ATTEMPTS")
                ChallengeCookies.Delete(httpContext, challengeCookies.Login);

            return Results.Redirect($"/admin/login-2fa?error={Uri.EscapeDataString(code)}");
        }

        ChallengeCookies.Delete(httpContext, challengeCookies.Login);
        return await CompleteSignInAsync(httpContext, apiResponse.Data.AccessToken, "/admin/login");
    }

    /// <summary>
    /// Reenvía el código. La API emite un reto nuevo, así que la cookie se refresca con él;
    /// si no devuelve ninguno, la anterior sigue siendo la válida y se deja como está.
    /// </summary>
    public static async Task<IResult> ResendTwoFactorAsync(
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        [Microsoft.AspNetCore.Mvc.FromServices] ChallengeCookieNames challengeCookies,
        CancellationToken ct)
    {
        var challengeToken = ChallengeCookies.Read(httpContext, challengeCookies.Login);
        if (string.IsNullOrWhiteSpace(challengeToken))
            return SessionExpired(httpContext, challengeCookies);

        var httpClient = httpClientFactory.CreateClient("AuthApi");
        var response = await httpClient.PostAsJsonAsync("api/v1/auth/two-factor/resend",
            new { ChallengeToken = challengeToken }, ct);

        var apiResponse = await ReadAuthResponseAsync(response, ct);

        if (!response.IsSuccessStatusCode || apiResponse?.Success != true || apiResponse.Data is null)
        {
            var code = ErrorCodeOf(response, apiResponse, "CHANNEL_UNAVAILABLE");
            if (code == "INVALID_CHALLENGE")
                ChallengeCookies.Delete(httpContext, challengeCookies.Login);

            return Results.Redirect($"/admin/login-2fa?error={Uri.EscapeDataString(code)}");
        }

        if (!string.IsNullOrWhiteSpace(apiResponse.Data.ChallengeToken))
            ChallengeCookies.Set(httpContext, challengeCookies.Login, apiResponse.Data.ChallengeToken!);

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
        [Microsoft.AspNetCore.Mvc.FromServices] ChallengeCookieNames challengeCookies,
        CancellationToken ct)
    {
        var enrollmentToken = ChallengeCookies.Read(httpContext, challengeCookies.Enrollment);
        if (string.IsNullOrWhiteSpace(enrollmentToken))
            return SessionExpired(httpContext, challengeCookies);

        var httpClient = httpClientFactory.CreateClient("AuthApi");
        var response = await httpClient.PostAsJsonAsync("api/v1/auth/two-factor/enroll/confirm",
            new { EnrollmentToken = enrollmentToken, Code = request.Code ?? string.Empty }, ct);

        var apiResponse = await ReadAuthResponseAsync(response, ct);

        if (!response.IsSuccessStatusCode || apiResponse?.Success != true || apiResponse.Data is null)
        {
            var code = ErrorCodeOf(response, apiResponse, "CODE_INVALID");
            if (code is "INVALID_CHALLENGE" or "CODE_EXPIRED" or "TOO_MANY_ATTEMPTS")
                ChallengeCookies.Delete(httpContext, challengeCookies.Enrollment);

            return Results.Redirect($"/admin/enroll-authenticator?error={Uri.EscapeDataString(code)}");
        }

        ChallengeCookies.Delete(httpContext, challengeCookies.Enrollment);
        return await CompleteSignInAsync(httpContext, apiResponse.Data.AccessToken, "/admin/login");
    }

    public static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        [Microsoft.AspNetCore.Mvc.FromServices] ChallengeCookieNames challengeCookies)
    {
        ChallengeCookies.Delete(httpContext, challengeCookies.Login);
        ChallengeCookies.Delete(httpContext, challengeCookies.Enrollment);
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

    /// <summary>
    /// Sin cookie no hay nada que canjear: caducó o alguien entró directo a la URL. Vuelta al
    /// login con un código que esa página ya sabe mostrar.
    /// </summary>
    private static IResult SessionExpired(
        HttpContext httpContext, ChallengeCookieNames challengeCookies)
    {
        ChallengeCookies.Delete(httpContext, challengeCookies.Login);
        ChallengeCookies.Delete(httpContext, challengeCookies.Enrollment);
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
