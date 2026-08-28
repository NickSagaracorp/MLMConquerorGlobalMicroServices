namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

/// <summary>
/// Alta del teléfono para el canal SMS del 2FA. Solo lleva el número: el usuario sale del token
/// de acceso, nunca del cuerpo, para que nadie pueda cambiar el teléfono de otra cuenta.
/// </summary>
public class AddPhoneRequest
{
    /// <summary>Formato E.164: '+' y de 8 a 15 dígitos, sin espacios ni separadores.</summary>
    public string PhoneE164 { get; set; } = string.Empty;
}
