using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ChangePassword;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ForgotPassword;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Login;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Logout;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.RefreshToken;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ResetPassword;
using System.Security.Claims;

namespace MLMConquerorGlobalEdition.SignupAPI.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator       _mediator;
    private readonly IJwtService     _jwt;

    public AuthController(IMediator mediator, IJwtService jwt)
    {
        _mediator = mediator;
        _jwt      = jwt;
    }

    /// <summary>Authenticates a user; refresh token is set as an HttpOnly cookie.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new LoginCommand(request), ct);
        if (!result.IsSuccess)
            return Unauthorized(ApiResponse<AuthResponse>.Fail(result.ErrorCode!, result.Error!));

        var response = result.Value!;
        SetRefreshTokenCookie(response.RefreshToken);
        response.RefreshToken = string.Empty; // do not expose in response body
        return Ok(ApiResponse<AuthResponse>.Ok(response));
    }

    /// <summary>Issues new access token using the HttpOnly refresh cookie.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var rawToken = Request.Cookies["refresh_token"];
        if (string.IsNullOrEmpty(rawToken))
            return Unauthorized(ApiResponse<AuthResponse>.Fail("INVALID_REFRESH_TOKEN", "Refresh token missing."));

        var result = await _mediator.Send(new RefreshTokenCommand(rawToken), ct);
        if (!result.IsSuccess)
            return Unauthorized(ApiResponse<AuthResponse>.Fail(result.ErrorCode!, result.Error!));

        var response = result.Value!;
        SetRefreshTokenCookie(response.RefreshToken);
        response.RefreshToken = string.Empty;
        return Ok(ApiResponse<AuthResponse>.Ok(response));
    }

    /// <summary>Logs out the current user, invalidates the DB token, and clears the cookie.</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? string.Empty;
        var result = await _mediator.Send(new LogoutCommand(userId), ct);
        Response.Cookies.Delete("refresh_token");
        return Ok(ApiResponse<bool>.Ok(result.Value));
    }

    /// <summary>Sends a password reset email. Always returns 200 to prevent email enumeration.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken ct)
    {
        await _mediator.Send(new ForgotPasswordCommand(request.Email), ct);
        return Ok(ApiResponse<bool>.Ok(true, "If an account exists for this email, a reset link has been sent."));
    }

    /// <summary>Resets a user's password using the token received by email.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new ResetPasswordCommand(request), ct);
        return result.IsSuccess
            ? Ok(ApiResponse<bool>.Ok(true, "Password reset successfully."))
            : BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
    }

    /// <summary>Changes the authenticated user's password.</summary>
    [HttpPut("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? string.Empty;
        var result = await _mediator.Send(new ChangePasswordCommand(userId, request), ct);
        return result.IsSuccess
            ? Ok(ApiResponse<bool>.Ok(true, "Password changed successfully."))
            : BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
    }


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
}
