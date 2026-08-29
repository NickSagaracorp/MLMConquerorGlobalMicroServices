using System.Net.Http.Json;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using MLMConquerorGlobalEdition.SharedComponents.Server.Services;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenterWeb.Services;

/// <summary>
/// Lo de ENTRAR al centro de negocios: login, segundo factor y salida.
/// </summary>
/// <remarks>
/// La cookie del reto se escribe y se lee con <see cref="ChallengeCookies"/>, la misma clase que
/// usa el área de cuenta compartida, y su nombre llega inyectado en
/// <see cref="ChallengeCookieNames"/> desde <c>Program.cs</c>. Antes esto tenía su propia constante
/// <c>2fa_challenge</c> y su propio juego de <c>CookieOptions</c>: el día que este portal montase la
/// superficie de cuenta compartida habría escrito el reto con un nombre y la pantalla de
/// verificación lo habría buscado con otro, sin que fallase ni el compilador ni las pruebas.
/// </remarks>
public static class AuthEndpoints
{
    public static async Task<IResult> LoginAsync(
        [Microsoft.AspNetCore.Mvc.FromForm] LoginRequest request,
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        [Microsoft.AspNetCore.Mvc.FromServices] ChallengeCookieNames challengeCookies,
        CancellationToken ct)
    {
        var httpClient = httpClientFactory.CreateClient("AuthApi");
        var response = await httpClient.PostAsJsonAsync("api/v1/auth/login",
            new { request.Email, request.Password }, ct);

        if (!response.IsSuccessStatusCode)
            return Results.Redirect("/login?error=invalid");

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<AuthTokens>>(cancellationToken: ct);
        if (apiResponse?.Success != true || apiResponse.Data is null)
            return Results.Redirect("/login?error=invalid");

        // 2FA branch — backend issued a challenge token instead of access tokens.
        // Persist the challenge in a short-lived HttpOnly cookie and redirect to
        // the verification page. The user's email is passed in the URL so the
        // page can show "code sent to j***@example.com".
        if (apiResponse.Data.RequiresTwoFactor && !string.IsNullOrEmpty(apiResponse.Data.ChallengeToken))
        {
            ChallengeCookies.Set(httpContext, challengeCookies.Login, apiResponse.Data.ChallengeToken);
            var emailParam = Uri.EscapeDataString(apiResponse.Data.Email ?? string.Empty);
            return Results.Redirect($"/two-factor?email={emailParam}");
        }

        return await CompleteSignInAsync(httpContext, apiResponse.Data);
    }

    public static async Task<IResult> VerifyTwoFactorAsync(
        [Microsoft.AspNetCore.Mvc.FromForm] VerifyTwoFactorForm form,
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        [Microsoft.AspNetCore.Mvc.FromServices] ChallengeCookieNames challengeCookies,
        CancellationToken ct)
    {
        var challengeToken = ChallengeCookies.Read(httpContext, challengeCookies.Login);
        if (string.IsNullOrEmpty(challengeToken))
            return Results.Redirect("/login?error=session_expired");

        if (string.IsNullOrWhiteSpace(form.Code) || form.Code.Length != 6 || !form.Code.All(char.IsDigit))
            return Results.Redirect("/two-factor?error=invalid_code");

        var httpClient = httpClientFactory.CreateClient("AuthApi");
        var response = await httpClient.PostAsJsonAsync("api/v1/auth/two-factor/verify",
            new { ChallengeToken = challengeToken, form.Code }, ct);

        if (!response.IsSuccessStatusCode)
        {
            var failed = await TryReadApi(response, ct);
            var code = failed?.ErrorCode ?? "invalid_code";
            // Expired or invalid challenge → bounce back to login (cookie cleared).
            if (code is "INVALID_CHALLENGE" or "CODE_EXPIRED")
            {
                ChallengeCookies.Delete(httpContext, challengeCookies.Login);
                return Results.Redirect("/login?error=session_expired");
            }
            return Results.Redirect("/two-factor?error=invalid_code");
        }

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<AuthTokens>>(cancellationToken: ct);
        if (apiResponse?.Success != true || apiResponse.Data is null)
            return Results.Redirect("/two-factor?error=invalid_code");

        ChallengeCookies.Delete(httpContext, challengeCookies.Login);
        return await CompleteSignInAsync(httpContext, apiResponse.Data);
    }

    public static async Task<IResult> ResendTwoFactorAsync(
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        [Microsoft.AspNetCore.Mvc.FromServices] ChallengeCookieNames challengeCookies,
        CancellationToken ct)
    {
        var challengeToken = ChallengeCookies.Read(httpContext, challengeCookies.Login);
        if (string.IsNullOrEmpty(challengeToken))
            return Results.Redirect("/login?error=session_expired");

        var httpClient = httpClientFactory.CreateClient("AuthApi");
        var response = await httpClient.PostAsJsonAsync("api/v1/auth/two-factor/resend",
            new { ChallengeToken = challengeToken }, ct);

        if (!response.IsSuccessStatusCode)
        {
            ChallengeCookies.Delete(httpContext, challengeCookies.Login);
            return Results.Redirect("/login?error=session_expired");
        }

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<AuthTokens>>(cancellationToken: ct);
        if (apiResponse?.Success != true || apiResponse.Data is null
            || string.IsNullOrEmpty(apiResponse.Data.ChallengeToken))
        {
            ChallengeCookies.Delete(httpContext, challengeCookies.Login);
            return Results.Redirect("/login?error=session_expired");
        }

        ChallengeCookies.Set(httpContext, challengeCookies.Login, apiResponse.Data.ChallengeToken);
        var emailParam = Uri.EscapeDataString(apiResponse.Data.Email ?? string.Empty);
        return Results.Redirect($"/two-factor?email={emailParam}&resent=1");
    }

    public static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        [Microsoft.AspNetCore.Mvc.FromServices] ChallengeCookieNames challengeCookies)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        ChallengeCookies.Delete(httpContext, challengeCookies.Login);
        return Results.Redirect("/login");
    }

    private static async Task<IResult> CompleteSignInAsync(HttpContext httpContext, AuthTokens tokens)
    {
        var handler = new JwtSecurityTokenHandler();
        if (string.IsNullOrEmpty(tokens.AccessToken) || !handler.CanReadToken(tokens.AccessToken))
            return Results.Redirect("/login?error=invalid");

        var jwt    = handler.ReadJwtToken(tokens.AccessToken);
        var claims = jwt.Claims.ToList();
        claims.Add(new Claim("access_token", tokens.AccessToken));

        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });

        var appCode = jwt.Claims.FirstOrDefault(c => c.Type == "default_language")?.Value;
        if (!string.IsNullOrWhiteSpace(appCode) && LanguageCodeMapper.IsSupportedAppCode(appCode))
        {
            var cultureName = LanguageCodeMapper.ToCultureName(appCode);
            httpContext.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(
                    new RequestCulture(cultureName, cultureName)),
                new CookieOptions
                {
                    Expires  = DateTimeOffset.UtcNow.AddYears(1),
                    SameSite = SameSiteMode.Lax,
                    Path     = "/"
                });
        }

        return Results.Redirect("/");
    }

    private static async Task<ApiResponse<AuthTokens>?> TryReadApi(HttpResponseMessage response, CancellationToken ct)
    {
        try { return await response.Content.ReadFromJsonAsync<ApiResponse<AuthTokens>>(cancellationToken: ct); }
        catch { return null; }
    }

    public record LoginRequest(string Email, string Password);
    public record VerifyTwoFactorForm(string Code);

    private record AuthTokens(
        string AccessToken,
        string RefreshToken,
        DateTime TokenExpiry,
        bool RequiresTwoFactor,
        string? ChallengeToken,
        string? Email,
        string? MaskedEmail);
}
