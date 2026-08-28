namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

/// <summary>
/// Confirma el enrolamiento con el primer código que produce la aplicación del usuario. El
/// mismo <see cref="EnrollmentToken"/> que abrió el enrolamiento lo cierra.
/// </summary>
public class ConfirmEnrollmentRequest
{
    public string EnrollmentToken { get; set; } = string.Empty;
    public string Code            { get; set; } = string.Empty;
}
