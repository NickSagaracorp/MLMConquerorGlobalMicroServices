using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedKernel;
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
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator) => _mediator = mediator;

    /// <summary>Authenticates a user and returns JWT tokens.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new LoginCommand(request), ct);
        return result.IsSuccess
            ? Ok(ApiResponse<AuthResponse>.Ok(result.Value!))
            : Unauthorized(ApiResponse<AuthResponse>.Fail(result.ErrorCode!, result.Error!));
    }

    /// <summary>Issues new tokens using a valid refresh token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new RefreshTokenCommand(request.RefreshToken), ct);
        return result.IsSuccess
            ? Ok(ApiResponse<AuthResponse>.Ok(result.Value!))
            : Unauthorized(ApiResponse<AuthResponse>.Fail(result.ErrorCode!, result.Error!));
    }

    /// <summary>Logs out the current user by invalidating the refresh token.</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? string.Empty;
        var result = await _mediator.Send(new LogoutCommand(userId), ct);
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
}
