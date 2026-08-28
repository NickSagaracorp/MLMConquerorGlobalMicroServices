using MediatR;
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Phone;

/// <summary>Da de baja el teléfono del canal SMS del 2FA.</summary>
public class RemovePhoneHandler : IRequestHandler<RemovePhoneCommand, Result<bool>>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public RemovePhoneHandler(UserManager<ApplicationUser> userManager)
        => _userManager = userManager;

    public async Task<Result<bool>> Handle(RemovePhoneCommand command, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(command.UserId);
        if (user is null || !user.IsActive)
            return Result<bool>.Failure("USER_NOT_FOUND", "User not found.");

        // Los tres campos van juntos. Dejar el cifrado sin la marca —o al revés— deja un teléfono
        // a medio borrar: PII que ya nadie usa y un estado que ningún camino del 2FA sabe leer.
        user.TwoFactorPhoneEncrypted = null;
        user.TwoFactorPhoneLast4     = null;
        user.TwoFactorPhoneConfirmed = false;

        // Quitar el teléfono dejando SMS como canal preferido dejaría al usuario con un canal sin
        // destino: su siguiente inicio de sesión pediría un código por SMS, ResolveTarget
        // devolvería null y todo terminaría en CHANNEL_UNAVAILABLE, sin código y sin manera de
        // entrar. El correo siempre está —es el que identifica la cuenta—, así que ahí vuelve.
        //
        // Authenticator no se toca: ese segundo factor no depende del teléfono que se borra.
        if (user.PreferredTwoFactorChannel == TwoFactorChannel.Sms)
            user.PreferredTwoFactorChannel = TwoFactorChannel.Email;

        await _userManager.UpdateAsync(user);

        return Result<bool>.Success(true);
    }
}
