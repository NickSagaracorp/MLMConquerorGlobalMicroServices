using System.Globalization;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.SignupAPI.Services;

/// <summary>
/// La espera tras un cambio de factor. Con un token de acceso robado se puede primero reapuntar el
/// canal del código —cambiar el correo, enrolar un autenticador propio, confirmar un teléfono
/// propio— y luego pedir el step-up, que llegaría al atacante. La espera le quita esa secuencia:
/// durante <see cref="ConfigKey"/> horas desde el último cambio no hay step-up, y el aviso que
/// recibe el dueño en ese tiempo le da margen para reaccionar.
/// </summary>
public static class StepUpCooldown
{
    public const string ConfigKey    = "Auth:StepUp:FactorChangeCooldownHours";
    public const int    DefaultHours = 24;

    /// <summary>
    /// Hasta cuándo dura la espera, o null si no hay espera. Hora de servidor, igual que la marca.
    /// </summary>
    /// <remarks>
    /// Un valor de configuración inválido, cero o negativo vuelve al de por defecto: apagar la
    /// espera por un error de tecleo abriría justo el hueco que cierra.
    /// </remarks>
    public static DateTime? ActiveUntil(ApplicationUser user, IConfiguration config, DateTime now)
    {
        if (user.SecurityFactorsChangedAt is not { } changedAt)
            return null;

        var hours = int.TryParse(config[ConfigKey], out var configured) && configured > 0
            ? configured
            : DefaultHours;

        var until = changedAt.AddHours(hours);
        return until > now ? until : null;
    }

    /// <summary>
    /// El rechazo. La fecha va en el mensaje con formato ISO para que la UI pueda mostrarla sin
    /// adivinar el formato; <c>Result</c> no lleva datos en un fallo.
    /// </summary>
    public static Result<T> Failure<T>(DateTime until) => Result<T>.Failure(
        StepUpGrant.CooldownErrorCode,
        "Un factor de seguridad de la cuenta cambió hace poco; los cambios sensibles vuelven a estar " +
        $"disponibles a partir de {until.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)}.");
}
