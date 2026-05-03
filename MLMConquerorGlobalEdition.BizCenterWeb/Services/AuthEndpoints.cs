using System.Net.Http.Json;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenterWeb.Services;

public static class AuthEndpoints
{
    private const string TwoFactorCookieName = "2fa_challenge";

    public static async Task<IResult> LoginAsync(
        [Microsoft.AspNetCore.Mvc.FromForm] LoginRequest request,
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
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
            httpContext.Response.Cookies.Append(TwoFactorCookieName, apiResponse.Data.ChallengeToken, BuildChallengeCookieOptions());
            var emailParam = Uri.EscapeDataString(apiResponse.Data.Email ?? string.Empty);
            return Results.Redirect($"/two-factor?email={emailParam}");
        }

        return await CompleteSignInAsync(httpContext, apiResponse.Data);
    }

    public static async Task<IResult> VerifyTwoFactorAsync(
        [Microsoft.AspNetCore.Mvc.FromForm] VerifyTwoFactorForm form,
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var challengeToken = httpContext.Request.Cookies[TwoFactorCookieName];
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
                ClearChallengeCookie(httpContext);
                return Results.Redirect("/login?error=session_expired");
            }
            return Results.Redirect("/two-factor?error=invalid_code");
        }

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<AuthTokens>>(cancellationToken: ct);
        if (apiResponse?.Success != true || apiResponse.Data is null)
            return Results.Redirect("/two-factor?error=invalid_code");

        ClearChallengeCookie(httpContext);
        return await CompleteSignInAsync(httpContext, apiResponse.Data);
    }

    public static async Task<IResult> ResendTwoFactorAsync(
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var challengeToken = httpContext.Request.Cookies[TwoFactorCookieName];
        if (string.IsNullOrEmpty(challengeToken))
            return Results.Redirect("/login?error=session_expired");

        var httpClient = httpClientFactory.CreateClient("AuthApi");
        var response = await httpClient.PostAsJsonAsync("api/v1/auth/two-factor/resend",
            new { ChallengeToken = challengeToken }, ct);

        if (!response.IsSuccessStatusCode)
        {
            ClearChallengeCookie(httpContext);
            return Results.Redirect("/login?error=session_expired");
        }

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<AuthTokens>>(cancellationToken: ct);
        if (apiResponse?.Success != true || apiResponse.Data is null
            || string.IsNullOrEmpty(apiResponse.Data.ChallengeToken))
        {
            ClearChallengeCookie(httpContext);
            return Results.Redirect("/login?error=session_expired");
        }

        httpContext.Response.Cookies.Append(TwoFactorCookieName, apiResponse.Data.ChallengeToken, BuildChallengeCookieOptions());
        var emailParam = Uri.EscapeDataString(apiResponse.Data.Email ?? string.Empty);
        return Results.Redirect($"/two-factor?email={emailParam}&resent=1");
    }

    public static async Task<IResult> LogoutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        ClearChallengeCookie(httpContext);
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

    private static CookieOptions BuildChallengeCookieOptions() => new()
    {
        HttpOnly = true,
        Secure   = true,
        SameSite = SameSiteMode.Lax,
        Path     = "/",
        Expires  = DateTimeOffset.UtcNow.AddMinutes(10)
    };

    private static void ClearChallengeCookie(HttpContext httpContext)
        => httpContext.Response.Cookies.Delete(TwoFactorCookieName, new CookieOptions { Path = "/" });

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
