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
    /// Handles the HTML form POST from Login.razor.
    /// Validates credentials against SignupAPI, sets the auth cookie, and redirects.
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

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<AuthTokens>>(cancellationToken: ct);
        if (apiResponse?.Success != true || apiResponse.Data is null)
            return Results.Redirect("/admin/login?error=invalid");

        // Parse the JWT to extract claims for the cookie identity
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(apiResponse.Data.AccessToken))
            return Results.Redirect("/admin/login?error=invalid");

        var jwt    = handler.ReadJwtToken(apiResponse.Data.AccessToken);
        var claims = jwt.Claims.ToList();
        claims.Add(new Claim("access_token", apiResponse.Data.AccessToken));

        // Ensure the user has at least one admin-level role
        var roles = claims
            .Where(c => c.Type == ClaimTypes.Role ||
                        c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
            .Select(c => c.Value)
            .ToList();

        var adminRoles = new[] { "SuperAdmin", "Admin", "CommissionManager",
                                  "BillingManager", "SupportManager",
                                  "SupportLevel1", "SupportLevel2", "SupportLevel3", "IT" };

        if (!roles.Any(r => adminRoles.Contains(r)))
            return Results.Redirect("/admin/login?error=access_denied");

        // Build ClaimsPrincipal and sign in — httpContext here IS the browser's real request
        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });

        return Results.Redirect("/admin");
    }

    public static async Task<IResult> LogoutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/admin/login");
    }

    public record LoginRequest(string Email, string Password);
    private record AuthTokens(string AccessToken, string RefreshToken, DateTime TokenExpiry);
}
