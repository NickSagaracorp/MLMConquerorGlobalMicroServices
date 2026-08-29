using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.AccountEnrollment;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ChangePassword;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.EmailConfirmation;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Enrollment;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ForgotPassword;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Login;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Logout;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Phone;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.RefreshToken;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ResendTwoFactor;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ResetPassword;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.SetPassword;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.TwoFactorSettings;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.VerifyTwoFactor;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Queries.AccountStatus;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Queries.PersonalData;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    /// <summary>
    /// Abre el enrolamiento TOTP para el usuario <b>ya autenticado</b>: devuelve la clave
    /// compartida, el URI <c>otpauth://</c> y su QR.
    /// </summary>
    /// <remarks>
    /// Ruta aparte de <see cref="BeginEnrollment"/> y no un endpoint híbrido. Aquel exige un
    /// <c>EnrollmentToken</c> que solo emite el login cuando fuerza el enrolamiento; este exige
    /// sesión. Son dos modelos de autenticación distintos, y juntarlos en una sola ruta obligaría
    /// a quien la lea a sostener los dos a la vez para saber quién puede llegar hasta ahí.
    ///
    /// Es lo que le faltaba a la pantalla de seguridad: un usuario que ya entró no tiene ningún
    /// token de enrolamiento, así que no podía activar ni volver a enrolar su autenticador.
    /// Volver a llamar estando ya enrolado devuelve una clave nueva — eso es re-enrolar.
    /// </remarks>
    [HttpPost("account/two-factor/enroll/begin")]
    [Authorize]
    public async Task<IActionResult> BeginAccountEnrollment(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? string.Empty;
        var result = await _mediator.Send(new BeginAccountEnrollmentCommand(userId), ct);
        return result.IsSuccess
            ? Ok(ApiResponse<EnrollmentResponse>.Ok(result.Value!))
            : BadRequest(ApiResponse<EnrollmentResponse>.Fail(result.ErrorCode!, result.Error!));
    }

    /// <summary>
    /// Cierra el enrolamiento del usuario autenticado con el primer código de su aplicación.
    /// </summary>
    /// <remarks>
    /// A diferencia de <see cref="ConfirmEnrollment"/>, <b>no emite tokens de acceso ni toca la
    /// cookie de refresco</b>: quien llega aquí ya tiene sesión, así que un juego de tokens nuevo
    /// solo serviría para rotarle la sesión por haber cambiado un ajuste de su cuenta.
    /// </remarks>
    [HttpPost("account/two-factor/enroll/confirm")]
    [Authorize]
    public async Task<IActionResult> ConfirmAccountEnrollment(
        [FromBody] ConfirmAccountEnrollmentRequest request,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? string.Empty;
        var result = await _mediator.Send(new ConfirmAccountEnrollmentCommand(userId, request), ct);
        return result.IsSuccess
            ? Ok(ApiResponse<bool>.Ok(true, "Two-factor authentication enabled."))
            : BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
    }

    /// <summary>Fija el canal por el que el usuario autenticado recibirá su código de 2FA.</summary>
    /// <remarks>
    /// El servidor comprueba que el canal tenga destino para esta cuenta y responde
    /// <c>CHANNEL_UNAVAILABLE</c> si no lo tiene, aunque la pantalla ya solo ofrezca los canales
    /// de <c>AvailableChannels</c>. Ese filtro es de presentación: quien llame a la API
    /// directamente podría fijarse SMS sin teléfono confirmado y quedarse sin poder entrar en su
    /// siguiente inicio de sesión, porque el código iría a un canal que no existe.
    /// </remarks>
    [HttpPost("two-factor/channel")]
    [Authorize]
    public async Task<IActionResult> SetTwoFactorChannel(
        [FromBody] SetTwoFactorChannelRequest request,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? string.Empty;
        var result = await _mediator.Send(new SetTwoFactorChannelCommand(userId, request), ct);
        return result.IsSuccess
            ? Ok(ApiResponse<bool>.Ok(true, "Preferred two-factor channel updated."))
            : BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
    }

    /// <summary>Desactiva el 2FA del usuario autenticado y reinicia su clave del autenticador.</summary>
    /// <remarks>
    /// El servidor rechaza con <c>TWO_FACTOR_REQUIRED</c> si el rol del usuario está en
    /// <c>Auth:TwoFactor:MandatoryRoles</c>, aunque la pantalla ya esconda el botón: esconder un
    /// botón no cierra la ruta, y lo que hay al otro lado es la política que obliga al personal
    /// con acceso al panel a llevar segundo factor.
    /// </remarks>
    [HttpPost("two-factor/disable")]
    [Authorize]
    public async Task<IActionResult> DisableTwoFactor(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? string.Empty;
        var result = await _mediator.Send(new DisableTwoFactorCommand(userId), ct);
        return result.IsSuccess
            ? Ok(ApiResponse<bool>.Ok(true, "Two-factor authentication disabled."))
            : BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
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


    /// <summary>
    /// Da de alta el teléfono del canal SMS y le manda el código que lo confirmará. El número
    /// queda guardado sin confirmar hasta que se redima ese código.
    /// </summary>
    /// <remarks>
    /// Autenticado: es gestión de la propia cuenta, el usuario ya entró. El teléfono se guarda
    /// contra el usuario de las claims, no contra ningún identificador del cuerpo — así nadie
    /// puede dar de alta un teléfono en la cuenta de otro y desviar ahí sus códigos.
    ///
    /// El SMS lo despacha <c>ITwoFactorService</c>, que aplica el tope de emisiones por usuario:
    /// aquí el número lo elige quien llama, así que sin ese tope el endpoint sería una forma de
    /// mandar SMS ilimitados a cualquier teléfono a costa de la empresa.
    /// </remarks>
    [HttpPost("phone")]
    [Authorize]
    public async Task<IActionResult> AddPhone(
        [FromBody] AddPhoneRequest request,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? string.Empty;
        var result = await _mediator.Send(new AddPhoneCommand(userId, request), ct);
        return result.IsSuccess
            ? Ok(ApiResponse<PhoneChallengeResponse>.Ok(result.Value!))
            : BadRequest(ApiResponse<PhoneChallengeResponse>.Fail(result.ErrorCode!, result.Error!));
    }

    /// <summary>Confirma el teléfono con el código recibido por SMS.</summary>
    /// <remarks>Autenticado por el mismo motivo que <see cref="AddPhone"/>.</remarks>
    [HttpPost("phone/verify")]
    [Authorize]
    public async Task<IActionResult> VerifyPhone(
        [FromBody] VerifyPhoneRequest request,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? string.Empty;
        var result = await _mediator.Send(new VerifyPhoneCommand(userId, request), ct);
        return result.IsSuccess
            ? Ok(ApiResponse<bool>.Ok(true, "Phone verified successfully."))
            : BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
    }

    /// <summary>
    /// Da de baja el teléfono del 2FA. Si el canal preferido era SMS, vuelve a correo: si no, la
    /// cuenta quedaría pidiendo códigos por un canal que ya no tiene destino.
    /// </summary>
    [HttpDelete("phone")]
    [Authorize]
    public async Task<IActionResult> RemovePhone(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? string.Empty;
        var result = await _mediator.Send(new RemovePhoneCommand(userId), ct);
        return result.IsSuccess
            ? Ok(ApiResponse<bool>.Ok(true, "Phone removed successfully."))
            : BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
    }


    /// <summary>
    /// Estado de la cuenta del usuario autenticado: correo, teléfono enmascarado, 2FA, canal
    /// preferido, canales disponibles y si la cuenta tiene contraseña.
    /// </summary>
    /// <remarks>
    /// El usuario sale de las claims, nunca de la query: si el identificador viniera de quien
    /// llama, cualquiera podría leer el estado de la cuenta de otro con solo cambiarlo.
    ///
    /// El teléfono se devuelve solo enmascarado. El número entero vive cifrado porque es a la vez
    /// PII y factor de autenticación; descifrarlo para la interfaz lo pondría en el tráfico y en
    /// los registros a cambio de nada, porque la pantalla solo enseña cuatro dígitos.
    /// </remarks>
    [HttpGet("account-status")]
    [Authorize]
    public async Task<IActionResult> AccountStatus(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? string.Empty;
        var result = await _mediator.Send(new GetAccountStatusQuery(userId), ct);
        return result.IsSuccess
            ? Ok(ApiResponse<AccountStatusResponse>.Ok(result.Value!))
            : BadRequest(ApiResponse<AccountStatusResponse>.Fail(result.ErrorCode!, result.Error!));
    }

    /// <summary>Los datos que el sistema guarda de la cuenta del usuario autenticado.</summary>
    /// <remarks>
    /// El usuario sale de las claims. Aquí importa más que en ningún otro endpoint: con el
    /// identificador en la query, esto descargaría los datos personales de cualquier cuenta.
    ///
    /// La respuesta se construye con una lista explícita de campos en el handler — nunca
    /// serializando la entidad y quitando lo que sobra. Fuera quedan el hash de la contraseña, el
    /// token de refresco, la clave del autenticador, el teléfono cifrado, el SecurityStamp y el
    /// ConcurrencyStamp: son material con el que se suplanta la cuenta, no datos del usuario.
    /// </remarks>
    [HttpGet("personal-data")]
    [Authorize]
    public async Task<IActionResult> PersonalData(CancellationToken ct)
    {
        var result = await LoadPersonalDataAsync(ct);
        return result.IsSuccess
            ? Ok(ApiResponse<PersonalDataResponse>.Ok(result.Value!))
            : BadRequest(ApiResponse<PersonalDataResponse>.Fail(result.ErrorCode!, result.Error!));
    }

    /// <summary>Los mismos datos que <see cref="PersonalData"/>, como archivo descargable.</summary>
    /// <remarks>
    /// El cuerpo va sin el sobre <c>ApiResponse</c>: lo que se guarda en disco es el archivo de
    /// datos del usuario, y meterle dentro los campos de transporte de la API lo ensuciaría sin
    /// aportar nada a quien lo abra.
    /// </remarks>
    [HttpGet("personal-data/download")]
    [Authorize]
    public async Task<IActionResult> DownloadPersonalData(CancellationToken ct)
    {
        var result = await LoadPersonalDataAsync(ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PersonalDataResponse>.Fail(result.ErrorCode!, result.Error!));

        var json = JsonSerializer.SerializeToUtf8Bytes(result.Value!, PersonalDataFileOptions);
        var fileName = $"personal-data-{DateTime.UtcNow:yyyy-MM-dd}.json";

        // File() pone Content-Disposition: attachment con este nombre.
        return File(json, "application/json", fileName);
    }

    /// <summary>
    /// Fija la primera contraseña de una cuenta que no tiene ninguna.
    /// </summary>
    /// <remarks>
    /// Hoy no hay logins externos, así que ninguna cuenta llega aquí sin contraseña y toda llamada
    /// termina en <c>PASSWORD_ALREADY_SET</c>. El endpoint existe porque estaba en el inventario
    /// acordado y porque un inicio de sesión con Google o Microsoft lo necesitaría: esas cuentas
    /// nacen sin contraseña local. No es código muerto por descuido.
    /// </remarks>
    [HttpPost("set-password")]
    [Authorize]
    public async Task<IActionResult> SetPassword(
        [FromBody] SetPasswordRequest request,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? string.Empty;
        var result = await _mediator.Send(new SetPasswordCommand(userId, request), ct);
        return result.IsSuccess
            ? Ok(ApiResponse<bool>.Ok(true, "Password set successfully."))
            : BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
    }


    /// <summary>Enums como texto y con sangría: el archivo lo abre una persona, no solo un cliente.</summary>
    private static readonly JsonSerializerOptions PersonalDataFileOptions = new()
    {
        WriteIndented = true,
        Converters    = { new JsonStringEnumConverter() }
    };

    private Task<Result<PersonalDataResponse>> LoadPersonalDataAsync(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? string.Empty;
        return _mediator.Send(new GetPersonalDataQuery(userId), ct);
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
