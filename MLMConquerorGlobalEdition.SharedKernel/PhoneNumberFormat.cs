using System.Text.RegularExpressions;

namespace MLMConquerorGlobalEdition.SharedKernel;

/// <summary>
/// La regla de formato E.164, en un solo sitio.
///
/// Vive aquí y no dentro del transporte de SMS porque hay dos puntos que la necesitan: quien da
/// de alta un teléfono (SignupAPI) y quien lo manda a Twilio (Notifications). Con una copia en
/// cada lado, la primera vez que una de las dos cambiara aceptaríamos números que después
/// fallarían al enviarse — y ese fallo aparece más tarde, cuando el usuario ya está esperando un
/// código que no va a llegar.
/// </summary>
public static partial class PhoneNumberFormat
{
    /// <summary>E.164: '+' seguido de 8 a 15 dígitos, sin espacios ni separadores.</summary>
    [GeneratedRegex(@"^\+\d{8,15}$")]
    private static partial Regex E164Pattern();

    public static bool IsE164(string? value) =>
        !string.IsNullOrWhiteSpace(value) && E164Pattern().IsMatch(value);
}
