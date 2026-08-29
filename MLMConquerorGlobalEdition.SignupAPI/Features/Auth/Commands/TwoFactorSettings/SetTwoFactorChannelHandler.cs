using MediatR;
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.TwoFactorSettings;

/// <summary>
/// Fija el canal por el que el usuario recibirá su código de segundo factor.
/// </summary>
/// <remarks>
/// <b>La disponibilidad se comprueba aquí, en el servidor, aunque la pantalla ya solo ofrezca los
/// canales de <c>AvailableChannels</c>.</b> Ese filtro es de presentación: quien llame a la API
/// directamente —con curl, con un cliente propio, o con la pantalla de una versión anterior en
/// caché— puede pedir SMS sin teléfono confirmado o Authenticator sin enrolar. Si el servidor lo
/// aceptara, el siguiente inicio de sesión pediría el código por un canal sin destino,
/// <c>ResolveTarget</c> devolvería null y la cuenta se quedaría fuera: el usuario se cierra la
/// puerta a sí mismo con una llamada que el servidor nunca debió aceptar.
///
/// La regla es la de <see cref="TwoFactorChannelAvailability"/>, la misma que usa
/// <c>GetAccountStatusHandler</c> para decir qué ofrecer, y no una copia: si las dos divergieran,
/// el servidor aceptaría exactamente lo que la pantalla ya no ofrece.
/// </remarks>
public class SetTwoFactorChannelHandler : IRequestHandler<SetTwoFactorChannelCommand, Result<bool>>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public SetTwoFactorChannelHandler(UserManager<ApplicationUser> userManager)
        => _userManager = userManager;

    public async Task<Result<bool>> Handle(SetTwoFactorChannelCommand command, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(command.UserId);
        if (user is null || !user.IsActive)
            return Result<bool>.Failure("USER_NOT_FOUND", "User not found.");

        var channel = command.Request.Channel;

        // Un valor fuera del enum tampoco está en la lista, así que cae por aquí sin necesitar
        // una comprobación aparte: lo que importa no es que el número sea un canal conocido,
        // sino que ese canal tenga a dónde mandar el código de esta cuenta.
        if (!TwoFactorChannelAvailability.IsAvailable(user, channel))
            return Result<bool>.Failure(
                "CHANNEL_UNAVAILABLE",
                "That two-factor channel has no destination for this account.");

        user.PreferredTwoFactorChannel = channel;
        await _userManager.UpdateAsync(user);

        return Result<bool>.Success(true);
    }
}
