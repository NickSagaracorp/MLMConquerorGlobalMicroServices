using System.Net.Http.Json;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenterWeb.Services;

public static class AuthEndpoints
{
    public static async Task<IResult> LoginAsync(
        [Microsoft.AspNetCore.Mvc.FromForm] LoginRequest request,
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        CancellationToken ct)
    {
        // Auth lives in the SignupAPI
        var httpClient = httpClientFactory.CreateClient("AuthApi");
        var response = await httpClient.PostAsJsonAsync("api/v1/auth/login",
            new { request.Email, request.Password }, ct);

        if (!response.IsSuccessStatusCode)
            return Results.Redirect("/login?error=invalid");

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<AuthTokens>>(cancellationToken: ct);
        if (apiResponse?.Success != true || apiResponse.Data is null)
            return Results.Redirect("/login?error=invalid");

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(apiResponse.Data.AccessToken))
            return Results.Redirect("/login?error=invalid");

        var jwt    = handler.ReadJwtToken(apiResponse.Data.AccessToken);
        var claims = jwt.Claims.ToList();
        claims.Add(new Claim("access_token", apiResponse.Data.AccessToken));

        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });

        return Results.Redirect("/");
    }

    public static async Task<IResult> LogoutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/login");
    }

    public record LoginRequest(string Email, string Password);
    private record AuthTokens(string AccessToken, string RefreshToken, DateTime TokenExpiry);
}
