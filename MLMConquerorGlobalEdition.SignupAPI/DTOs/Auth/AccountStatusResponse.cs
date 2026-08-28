using MLMConquerorGlobalEdition.Domain.Entities.Security;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

/// <summary>
/// Todo lo que el panel de gestión de la cuenta necesita para pintarse de una sola llamada:
/// qué está confirmado, qué segundo factor hay puesto y qué canales tienen destino.
/// </summary>
/// <remarks>
/// El teléfono sale <b>solo enmascarado</b>. El número entero vive cifrado en
/// <c>TwoFactorPhoneEncrypted</c> porque es a la vez PII y factor de autenticación;
/// descifrarlo para mandarlo a la interfaz lo pondría en el tráfico y en cualquier registro
/// intermedio sin ninguna necesidad, porque la pantalla solo enseña los cuatro últimos dígitos.
/// Para eso existe <c>TwoFactorPhoneLast4</c>, que se guarda en claro a propósito.
/// </remarks>
public class AccountStatusResponse
{
    public string Email          { get; set; } = string.Empty;
    public bool   EmailConfirmed { get; set; }

    /// <summary>Asteriscos y los cuatro últimos dígitos. Null si la cuenta no tiene teléfono.</summary>
    public string? MaskedPhone { get; set; }

    public bool HasPhone       { get; set; }
    public bool PhoneConfirmed { get; set; }

    public bool             TwoFactorEnabled          { get; set; }
    public TwoFactorChannel PreferredTwoFactorChannel { get; set; }
    public DateTime?        TwoFactorEnrolledAt       { get; set; }

    /// <summary>Si la cuenta tiene contraseña. Falso solo para cuentas creadas por un login externo.</summary>
    public bool HasPassword { get; set; }

    /// <summary>
    /// Canales con destino real para este usuario. La pantalla no debe ofrecer ninguno que no
    /// esté aquí: elegir un canal sin destino deja al usuario pidiendo un código que nunca sale
    /// y le cierra la puerta en su siguiente inicio de sesión.
    /// </summary>
    public IReadOnlyList<TwoFactorChannel> AvailableChannels { get; set; } = [];
}
