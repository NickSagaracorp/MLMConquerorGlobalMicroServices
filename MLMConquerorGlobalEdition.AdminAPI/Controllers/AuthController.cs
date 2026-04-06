using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtService                  _jwt;
    private readonly IDateTimeProvider            _dateTime;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        IJwtService                  jwt,
        IDateTimeProvider            dateTime)
    {
        _userManager = userManager;
        _jwt         = jwt;
        _dateTime    = dateTime;
    }

    /// <summary>POST /api/v1/auth/login — returns access token; refresh token set as HttpOnly cookie.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthTokensDto>>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
            return Unauthorized(ApiResponse<AuthTokensDto>.Fail("INVALID_CREDENTIALS", "Invalid email or password."));

        if (await _userManager.IsLockedOutAsync(user))
            return Unauthorized(ApiResponse<AuthTokensDto>.Fail("ACCOUNT_LOCKED", "Account is temporarily locked."));

        var valid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!valid)
        {
            await _userManager.AccessFailedAsync(user);
            return Unauthorized(ApiResponse<AuthTokensDto>.Fail("INVALID_CREDENTIALS", "Invalid email or password."));
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var roles        = await _userManager.GetRolesAsync(user);
        var accessToken  = _jwt.GenerateAccessToken(user.Id, user.MemberProfileId ?? string.Empty, user.Email!, roles);
        var refreshToken = _jwt.GenerateRefreshToken();
        var now          = _dateTime.Now;

        user.RefreshToken       = HashToken(refreshToken);
        user.RefreshTokenExpiry = now.Add(_jwt.RefreshTokenExpiry);
        user.LastLoginAt        = now;
        await _userManager.UpdateAsync(user);

        SetRefreshTokenCookie(refreshToken);

        return Ok(ApiResponse<AuthTokensDto>.Ok(new AuthTokensDto(accessToken, now.Add(_jwt.AccessTokenExpiry))));
    }

    /// <summary>POST /api/v1/auth/refresh — issues new access token using the HttpOnly refresh cookie.</summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<AuthTokensDto>>> Refresh(CancellationToken ct)
    {
        var rawToken = Request.Cookies["refresh_token"];
        if (string.IsNullOrEmpty(rawToken))
            return Unauthorized(ApiResponse<AuthTokensDto>.Fail("INVALID_REFRESH_TOKEN", "Refresh token missing."));

        var hashed = HashToken(rawToken);
        var now    = _dateTime.Now;

        var user = _userManager.Users.FirstOrDefault(u => u.RefreshToken == hashed);
        if (user is null || !user.IsActive)
            return Unauthorized(ApiResponse<AuthTokensDto>.Fail("INVALID_REFRESH_TOKEN", "Refresh token is invalid."));

        if (user.RefreshTokenExpiry < now)
            return Unauthorized(ApiResponse<AuthTokensDto>.Fail("REFRESH_TOKEN_EXPIRED", "Refresh token has expired. Please log in again."));

        var roles           = await _userManager.GetRolesAsync(user);
        var newAccessToken  = _jwt.GenerateAccessToken(user.Id, user.MemberProfileId ?? string.Empty, user.Email!, roles);
        var newRefreshToken = _jwt.GenerateRefreshToken();

        user.RefreshToken       = HashToken(newRefreshToken);
        user.RefreshTokenExpiry = now.Add(_jwt.RefreshTokenExpiry);
        await _userManager.UpdateAsync(user);

        SetRefreshTokenCookie(newRefreshToken);

        return Ok(ApiResponse<AuthTokensDto>.Ok(new AuthTokensDto(newAccessToken, now.Add(_jwt.AccessTokenExpiry))));
    }

    /// <summary>POST /api/v1/auth/logout — invalidates the refresh token and clears the cookie.</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");

        if (!string.IsNullOrEmpty(userId))
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is not null)
            {
                user.RefreshToken       = null;
                user.RefreshTokenExpiry = null;
                await _userManager.UpdateAsync(user);
            }
        }

        Response.Cookies.Delete("refresh_token");
        return Ok(ApiResponse<bool>.Ok(true, "Logged out successfully."));
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private void SetRefreshTokenCookie(string rawToken)
    {
        Response.Cookies.Append("refresh_token", rawToken, new CookieOptions
        {
            HttpOnly = true,
            Secure   = true,
            SameSite = SameSiteMode.Strict,
            Expires  = DateTimeOffset.UtcNow.Add(_jwt.RefreshTokenExpiry)
        });
    }

    private static string HashToken(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    public record LoginRequest(string Email, string Password);

    /// <summary>Refresh token is not returned in the body — it is set as an HttpOnly cookie.</summary>
    public record AuthTokensDto(string AccessToken, DateTime TokenExpiry);
}
