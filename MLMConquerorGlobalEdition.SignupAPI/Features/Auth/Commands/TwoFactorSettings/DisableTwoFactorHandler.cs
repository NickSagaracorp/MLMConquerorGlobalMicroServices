using MediatR;
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.TwoFactorSettings;

/// <summary>
/// Desactiva el segundo factor del usuario autenticado.
/// </summary>
/// <remarks>
/// <b>La obligatoriedad por rol se comprueba aquí, en el servidor, aunque la pantalla ya esconda
/// el botón.</b> Esconder un botón no es una restricción: la ruta sigue existiendo y una llamada
/// directa la alcanza igual. Y aquí lo que hay al otro lado no es un ajuste cosmético — es la
/// política que obliga al personal con acceso al panel a llevar segundo factor. Si el servidor
/// la aceptara, cualquiera de esas cuentas podría quedarse solo con contraseña y el
/// <c>MandatoryRoles</c> de la configuración no significaría nada.
///
/// Es la misma lista que lee <c>LoginHandler</c> para forzar el enrolamiento, así que rol y
/// política coinciden en los dos extremos del ciclo: el login obliga a enrolarse y este comando
/// impide deshacerlo.
///
/// El borrado va por <see cref="ITotpEnrollmentService.ResetAsync"/>, que apaga el 2FA, borra la
/// marca de enrolamiento y <b>reinicia la clave del autenticador</b>. Lo tercero es lo que se
/// olvida: dejar la clave viva significa que la entrada que el usuario tiene en su teléfono
/// —posiblemente en un aparato que ya no controla— seguiría siendo válida en cuanto volviera a
/// activar el 2FA, cuando él cree que la desactivó y la dio por muerta.
/// </remarks>
public class DisableTwoFactorHandler : IRequestHandler<DisableTwoFactorCommand, Result<bool>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITotpEnrollmentService       _enrollment;
    private readonly IConfiguration               _config;

    public DisableTwoFactorHandler(
        UserManager<ApplicationUser> userManager,
        ITotpEnrollmentService       enrollment,
        IConfiguration               config)
    {
        _userManager = userManager;
        _enrollment  = enrollment;
        _config      = config;
    }

    public async Task<Result<bool>> Handle(DisableTwoFactorCommand command, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(command.UserId);
        if (user is null || !user.IsActive)
            return Result<bool>.Failure("USER_NOT_FOUND", "User not found.");

        var roles          = await _userManager.GetRolesAsync(user);
        var mandatoryRoles = _config.GetSection("Auth:TwoFactor:MandatoryRoles").Get<string[]>() ?? [];

        // OrdinalIgnoreCase igual que en LoginHandler: la configuración la escribe una persona y
        // "admin" tiene que valer lo mismo que "Admin", o la política dejaría de aplicarse por
        // una mayúscula.
        if (roles.Any(r => mandatoryRoles.Contains(r, StringComparer.OrdinalIgnoreCase)))
            return Result<bool>.Failure(
                "TWO_FACTOR_REQUIRED",
                "Two-factor authentication is required for this account's role.");

        var reset = await _enrollment.ResetAsync(user, ct);
        if (!reset.IsSuccess)
            return Result<bool>.Failure(reset.ErrorCode!, reset.Error!);

        // ResetAsync acaba de borrar la clave del autenticador, así que Authenticator ya no está
        // entre los canales disponibles. Dejarlo como preferido es la misma incoherencia que
        // RemovePhoneHandler evita al quitar el teléfono: AccountStatus mostraría un canal
        // preferido que no aparece en su propia lista de disponibles, y quien volviera a activar
        // el 2FA sin re-enrolar arrancaría apuntando a un factor que no existe. El correo siempre
        // está —es el que identifica la cuenta—, así que ahí vuelve.
        if (user.PreferredTwoFactorChannel == TwoFactorChannel.Authenticator)
        {
            user.PreferredTwoFactorChannel = TwoFactorChannel.Email;
            await _userManager.UpdateAsync(user);
        }

        return Result<bool>.Success(true);
    }
}
