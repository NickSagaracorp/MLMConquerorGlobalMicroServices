using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Identity;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth;

/// <summary>
/// Qué canales de 2FA tienen destino real para un usuario concreto.
/// </summary>
/// <remarks>
/// Vive aparte y no dentro de <c>GetAccountStatusHandler</c> porque hay dos consumidores que
/// tienen que coincidir exactamente: la consulta que le dice a la pantalla qué puede ofrecer y
/// el comando que fija el canal preferido. Si cada uno llevara su propia copia de la regla,
/// bastaría con que una de las dos se quedara atrás para que el servidor aceptara un canal que
/// la pantalla ya no ofrece —o al revés— y el usuario terminara con un canal por el que no le
/// llega ningún código, es decir, sin poder entrar en su siguiente inicio de sesión.
///
/// Mismas condiciones que aplica <c>ResolveTarget</c> de la librería de 2FA al emitir el código.
/// </remarks>
public static class TwoFactorChannelAvailability
{
    /// <summary>Canales que tienen destino real para este usuario.</summary>
    public static IReadOnlyList<TwoFactorChannel> Resolve(ApplicationUser user)
    {
        // Correo siempre: es lo que identifica la cuenta, así que su destino existe por definición.
        var channels = new List<TwoFactorChannel> { TwoFactorChannel.Email };

        // SMS solo con el teléfono confirmado. Un número que nadie ha demostrado tener no es un
        // segundo factor, y la librería lo rechaza fuera del enrolamiento.
        if (!string.IsNullOrWhiteSpace(user.TwoFactorPhoneEncrypted) && user.TwoFactorPhoneConfirmed)
            channels.Add(TwoFactorChannel.Sms);

        // Authenticator solo con el enrolamiento confirmado: sin clave dada de alta no hay nada
        // que Identity pueda verificar y la pantalla del código no aceptaría ninguno.
        if (user.TwoFactorEnrolledAt is not null)
            channels.Add(TwoFactorChannel.Authenticator);

        return channels;
    }

    /// <summary>
    /// Si el canal pedido tiene destino. Un valor que no pertenece al enum tampoco lo tiene: no
    /// aparece en la lista, así que cae por el mismo camino sin necesitar una comprobación aparte.
    /// </summary>
    public static bool IsAvailable(ApplicationUser user, TwoFactorChannel channel)
        => Resolve(user).Contains(channel);
}
