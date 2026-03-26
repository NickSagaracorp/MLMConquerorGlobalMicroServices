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
        LoginRequest request,
        HttpClient httpClient,
        HttpContext httpContext,
        CancellationToken ct)
    {
        // Call the backend Signups API
        var response = await httpClient.PostAsJsonAsync("api/v1/auth/login", request, ct);
        if (!response.IsSuccessStatusCode)
            return Results.Unauthorized();

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<AuthTokens>>(cancellationToken: ct);
        if (apiResponse?.Success != true || apiResponse.Data is null)
            return Results.Unauthorized();

        // Parse the JWT to extract claims
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(apiResponse.Data.AccessToken))
            return Results.Unauthorized();

        var jwt    = handler.ReadJwtToken(apiResponse.Data.AccessToken);
        var claims = jwt.Claims.ToList();

        // Build ClaimsPrincipal and sign in with cookie
        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });

        return Results.Ok(new { success = true });
    }

    public static async Task<IResult> LogoutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Ok();
    }

    public record LoginRequest(string Email, string Password);
    private record AuthTokens(string AccessToken, string RefreshToken, DateTime TokenExpiry);
}
