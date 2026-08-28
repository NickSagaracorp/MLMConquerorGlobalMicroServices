namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

/// <summary>
/// Lo que el usuario necesita para dar de alta la cuenta en su aplicación de autenticación.
/// El 2FA no queda activo con esto: se activa al confirmar el primer código en
/// <c>POST /api/v1/auth/two-factor/enroll/confirm</c>.
/// </summary>
public class EnrollmentResponse
{
    /// <summary>El secreto en base32, para entrada manual si no se puede escanear el QR.</summary>
    public string SharedKey { get; set; } = string.Empty;

    /// <summary>URI <c>otpauth://</c> que codifica el QR.</summary>
    public string AuthenticatorUri { get; set; } = string.Empty;

    /// <summary>PNG en data-URI, listo para un <c>&lt;img src&gt;</c>.</summary>
    public string QrCodePngDataUri { get; set; } = string.Empty;
}
