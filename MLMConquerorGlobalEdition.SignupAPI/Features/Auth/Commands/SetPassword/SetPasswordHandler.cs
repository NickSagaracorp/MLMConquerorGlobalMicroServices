using MediatR;
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.SetPassword;

/// <summary>
/// Fija la primera contraseña de una cuenta que no tiene ninguna.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hoy este endpoint no tiene usuarios reales, y eso es a propósito.</b> Toda cuenta creada por
/// el alta lleva contraseña desde el primer momento, así que ahora mismo cualquier llamada aquí
/// termina en <c>PASSWORD_ALREADY_SET</c>. Está construido porque figuraba en el inventario de
/// endpoints acordado y porque es lo que hará falta el día que se acepte iniciar sesión con Google
/// o Microsoft: esas cuentas nacen sin contraseña local y necesitan una manera de ponerse la
/// primera sin tener ninguna anterior que demostrar. No es código muerto por descuido.
/// </para>
/// <para>
/// Se usa <c>AddPasswordAsync</c> y no <c>ChangePasswordAsync</c>: el segundo exige la contraseña
/// actual, que en este escenario no existe. Cuando la cuenta sí tiene una, el camino correcto es
/// <c>PUT /api/v1/auth/change-password</c>, que la pide — y por eso aquí se corta con un código
/// propio que dirige allí en vez de dejar que Identity devuelva un error genérico.
/// </para>
/// </remarks>
public class SetPasswordHandler : IRequestHandler<SetPasswordCommand, Result<bool>>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public SetPasswordHandler(UserManager<ApplicationUser> userManager)
        => _userManager = userManager;

    public async Task<Result<bool>> Handle(SetPasswordCommand command, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(command.UserId);
        if (user is null || !user.IsActive)
            return Result<bool>.Failure("USER_NOT_FOUND", "User not found.");

        // Con contraseña ya puesta esto sería fijar una nueva sin demostrar la anterior: quien se
        // hiciera con una sesión ajena se quedaría la cuenta sin conocerla. AddPasswordAsync ya lo
        // rechaza, pero con un mensaje genérico; el código propio deja claro a la interfaz que el
        // camino es cambiarla, no fijarla.
        if (await _userManager.HasPasswordAsync(user))
            return Result<bool>.Failure(
                "PASSWORD_ALREADY_SET",
                "La cuenta ya tiene contraseña. Usa el cambio de contraseña, que pide la actual.");

        var result = await _userManager.AddPasswordAsync(user, command.Request.NewPassword);
        if (!result.Succeeded)
        {
            // La política de contraseñas la aplica Identity; sus descripciones se propagan tal
            // cual para que el usuario sepa qué le falta a la que escribió.
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result<bool>.Failure("PASSWORD_SET_FAILED", errors);
        }

        // Mismo criterio que el cambio de contraseña: se invalidan los tokens de refresco. A
        // partir de aquí la cuenta tiene una credencial nueva, y las sesiones abiertas antes de
        // tenerla no deben sobrevivirla.
        user.RefreshToken       = null;
        user.RefreshTokenExpiry = null;
        await _userManager.UpdateAsync(user);

        return Result<bool>.Success(true);
    }
}
