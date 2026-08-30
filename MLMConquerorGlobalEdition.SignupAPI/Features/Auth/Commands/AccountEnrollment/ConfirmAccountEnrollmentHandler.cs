using MediatR;
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.AccountEnrollment;

/// <summary>
/// Cierra el enrolamiento TOTP desde una sesión ya iniciada, con el primer código de la
/// aplicación del usuario.
/// </summary>
/// <remarks>
/// <b>Aquí no se emiten tokens de acceso</b>, a diferencia de <c>ConfirmEnrollmentHandler</c>.
/// Aquel los emite porque su usuario venía de un login que se quedó a medias: había demostrado la
/// contraseña y no tenía sesión. El de aquí ya la tiene, así que devolver un juego de tokens
/// nuevo solo serviría para rotar la sesión de quien está cambiando un ajuste de su cuenta.
/// Devuelve el resultado y nada más.
///
/// La librería activa el 2FA y fija el canal preferido solo si el código es válido; si no lo es,
/// no toca nada.
///
/// <b>Y SE REVOCA LA SESIÓN VIVA</b>, que es lo que faltaba. Activar el segundo factor sin revocar
/// dejaba a la cuenta con un refresh token de treinta días emitido ANTES de que existiera ese
/// factor: quien lo tuviera —incluido quien no debía tenerlo, que es el motivo por el que el
/// usuario está activando el 2FA— seguía renovando su sesión un mes entero sin pasar nunca por el
/// código. Es decir, la medida no alcanzaba a nadie que ya estuviera dentro, que son exactamente
/// las sesiones de las que hay que protegerse. La regla y dónde está la línea, en
/// <see cref="SessionRevocation"/>.
/// </remarks>
public class ConfirmAccountEnrollmentHandler
    : IRequestHandler<ConfirmAccountEnrollmentCommand, Result<bool>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITotpEnrollmentService       _enrollment;

    public ConfirmAccountEnrollmentHandler(
        UserManager<ApplicationUser> userManager,
        ITotpEnrollmentService       enrollment)
    {
        _userManager = userManager;
        _enrollment  = enrollment;
    }

    public async Task<Result<bool>> Handle(
        ConfirmAccountEnrollmentCommand command, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(command.UserId);
        if (user is null || !user.IsActive)
            return Result<bool>.Failure("USER_NOT_FOUND", "User not found.");

        var confirmed = await _enrollment.ConfirmAsync(user, command.Request.Code, ct);
        if (!confirmed.IsSuccess)
            return Result<bool>.Failure(confirmed.ErrorCode!, confirmed.Error!);

        // DESPUÉS de confirmar y nunca antes: un código inválido no toca nada, así que tampoco
        // puede servirle a nadie para tirar la sesión de otro.
        await _userManager.RevokeLiveSessionsAsync(user);

        return Result<bool>.Success(true);
    }
}
