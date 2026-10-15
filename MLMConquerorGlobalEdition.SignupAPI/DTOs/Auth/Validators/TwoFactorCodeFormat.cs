namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth.Validators;

/// <summary>
/// El formato de un código de segundo factor (correo, SMS, TOTP y step-up): seis dígitos ASCII.
/// Una sola definición para los cinco validadores que reciben un código.
/// </summary>
/// <remarks>
/// No es <c>^\d{6}$</c>, que era lo que había, por dos detalles de las expresiones regulares de
/// .NET: <c>\d</c> acepta cualquier dígito Unicode (arábigo-índicos, de ancho completo…) y
/// <c>$</c> admite un salto de línea final. Ninguno de los dos es un código que el servidor haya
/// emitido. <c>TwoFactorService</c> aplica la misma regla por su cuenta —y cuenta el intento—, así
/// que esto es la primera barrera, no la única.
/// </remarks>
public static class TwoFactorCodeFormat
{
    public const string Pattern = @"^[0-9]{6}\z";
}
