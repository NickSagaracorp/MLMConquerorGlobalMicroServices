using System.Security.Cryptography;
using System.Text;
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

    /// <summary>POST /api/v1/auth/login</summary>
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

        user.RefreshToken        = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
        user.RefreshTokenExpiry  = _dateTime.Now.Add(_jwt.RefreshTokenExpiry);
        user.LastLoginAt         = _dateTime.Now;
        await _userManager.UpdateAsync(user);

        var expiry = _dateTime.Now.Add(_jwt.AccessTokenExpiry);
        return Ok(ApiResponse<AuthTokensDto>.Ok(new AuthTokensDto(accessToken, refreshToken, expiry)));
    }

    public record LoginRequest(string Email, string Password);
    public record AuthTokensDto(string AccessToken, string RefreshToken, DateTime TokenExpiry);
}
