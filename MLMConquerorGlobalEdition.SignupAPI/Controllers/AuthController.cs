using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ChangePassword;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.EmailConfirmation;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Enrollment;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ForgotPassword;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Login;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Logout;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.RefreshToken;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ResendTwoFactor;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ResetPassword;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.VerifyTwoFactor;
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

        // 2FA challenge — no refresh cookie yet; the client redeems the
        // ChallengeToken via /api/v1/auth/two-factor/verify to obtain real tokens.
        if (response.RequiresTwoFactor)
            return Ok(ApiResponse<AuthResponse>.Ok(response));

        SetRefreshTokenCookie(response.RefreshToken);
        response.RefreshToken = string.Empty; // do not expose in response body
        return Ok(ApiResponse<AuthResponse>.Ok(response));
    }

    /// <summary>Verifies a 6-digit TOTP-style code emitted by the login challenge and issues real access/refresh tokens.</summary>
    [HttpPost("two-factor/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyTwoFactor(
        [FromBody] VerifyTwoFactorRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new VerifyTwoFactorCommand(request), ct);
        if (!result.IsSuccess)
            return Unauthorized(ApiResponse<AuthResponse>.Fail(result.ErrorCode!, result.Error!));

        var response = result.Value!;
        SetRefreshTokenCookie(response.RefreshToken);
        response.RefreshToken = string.Empty;
        return Ok(ApiResponse<AuthResponse>.Ok(response));
    }

    /// <summary>Sends a fresh 6-digit code and returns a new ChallengeToken; rate-limited per IP via AspNetCoreRateLimit.</summary>
    [HttpPost("two-factor/resend")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendTwoFactor(
        [FromBody] ResendTwoFactorRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new ResendTwoFactorCommand(request), ct);
        return result.IsSuccess
            ? Ok(ApiResponse<AuthResponse>.Ok(result.Value!))
            : BadRequest(ApiResponse<AuthResponse>.Fail(result.ErrorCode!, result.Error!));
    }

    /// <summary>
    /// Abre el enrolamiento TOTP: devuelve la clave compartida, el URI otpauth:// y su QR.
    /// </summary>
    /// <remarks>
    /// Anónimo a propósito: el EnrollmentToken es la credencial. Quien llega aquí acaba de
    /// autenticarse con contraseña pero todavía no tiene tokens de acceso — precisamente porque
    /// le falta configurar el segundo factor que este endpoint da de alta.
    /// </remarks>
    [HttpPost("two-factor/enroll/begin")]
    [AllowAnonymous]
    public async Task<IActionResult> BeginEnrollment(
        [FromBody] BeginEnrollmentRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new BeginEnrollmentCommand(request), ct);
        return result.IsSuccess
            ? Ok(ApiResponse<EnrollmentResponse>.Ok(result.Value!))
            : Unauthorized(ApiResponse<EnrollmentResponse>.Fail(result.ErrorCode!, result.Error!));
    }

    /// <summary>
    /// Confirma el enrolamiento con el primer código de la aplicación del usuario y emite los
    /// tokens de acceso: queda dentro sin volver a iniciar sesión.
    /// </summary>
    /// <remarks>Anónimo por el mismo motivo que <see cref="BeginEnrollment"/>.</remarks>
    [HttpPost("two-factor/enroll/confirm")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEnrollment(
        [FromBody] ConfirmEnrollmentRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new ConfirmEnrollmentCommand(request), ct);
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

    /// <summary>
    /// Envía el correo de confirmación de dirección. Devuelve siempre 200, exista o no la
    /// cuenta y esté o no ya confirmada.
    /// </summary>
    /// <remarks>
    /// Anónimo a propósito: quien todavía no ha confirmado su correo no tiene por qué haber
    /// iniciado sesión. La respuesta es idéntica en todos los casos porque distinguirlos
    /// convertiría el endpoint en un oráculo para enumerar usuarios registrados — el mismo
    /// motivo por el que <see cref="ForgotPassword"/> responde así.
    /// </remarks>
    [HttpPost("email/send-confirmation")]
    [AllowAnonymous]
    public async Task<IActionResult> SendEmailConfirmation(
        [FromBody] SendEmailConfirmationRequest request,
        CancellationToken ct)
    {
        await _mediator.Send(new SendEmailConfirmationCommand(request.Email), ct);
        return Ok(ApiResponse<bool>.Ok(true, "If an account needs confirmation for this email, a confirmation link has been sent."));
    }

    /// <summary>Confirma la dirección de correo con el userId y el token del enlace recibido.</summary>
    /// <remarks>
    /// Anónimo porque el enlace del correo es la credencial. Aquí sí se falla explícitamente:
    /// quien llega con un enlace no está sondeando qué correos existen.
    /// </remarks>
    [HttpPost("email/confirm")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(
        [FromBody] ConfirmEmailRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new ConfirmEmailCommand(request), ct);
        return result.IsSuccess
            ? Ok(ApiResponse<bool>.Ok(true, "Email confirmed successfully."))
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
