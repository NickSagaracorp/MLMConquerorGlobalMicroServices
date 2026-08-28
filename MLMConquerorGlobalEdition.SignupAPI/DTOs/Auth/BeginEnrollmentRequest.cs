namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

/// <summary>
/// Petición de enrolamiento TOTP. El <see cref="EnrollmentToken"/> es la credencial: lo emite
/// el login cuando el rol exige 2FA y el usuario todavía no lo tiene configurado, así que el
/// endpoint es anónimo — quien se está enrolando aún no tiene tokens de acceso.
/// </summary>
public class BeginEnrollmentRequest
{
    public string EnrollmentToken { get; set; } = string.Empty;
}
