using MLMConquerorGlobalEdition.Domain.Entities.Security;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

public class AuthResponse
{
    public string MemberId     { get; set; } = string.Empty;
    public string UserId       { get; set; } = string.Empty;
    public string Email        { get; set; } = string.Empty;
    public string MemberType   { get; set; } = string.Empty;
    public string AccessToken  { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime TokenExpiry { get; set; }
    public IEnumerable<string> Roles { get; set; } = [];

    /// <summary>
    /// True when the user has two-factor authentication enabled and a code
    /// challenge has been issued instead of access tokens. Clients must call
    /// <c>POST /api/v1/auth/two-factor/verify</c> with the
    /// <see cref="ChallengeToken"/> and the 6-digit code received by email
    /// before the real <see cref="AccessToken"/>/<see cref="RefreshToken"/>
    /// are issued.
    /// </summary>
    public bool RequiresTwoFactor { get; set; }

    /// <summary>Short-lived JWT (5 min) carrying the hashed code; opaque to the client.</summary>
    public string? ChallengeToken { get; set; }

    /// <summary>Email with local part partially masked for UX (e.g. "j***@example.com").</summary>
    [Obsolete("Usa MaskedTarget; MaskedEmail solo se rellena cuando el canal es Email.")]
    public string? MaskedEmail { get; set; }

    /// <summary>
    /// True cuando el usuario tiene un rol que exige 2FA pero no lo ha configurado. El cliente
    /// debe llevarlo al enrolamiento con <see cref="EnrollmentToken"/>; no hay tokens de acceso
    /// hasta que lo complete.
    /// </summary>
    public bool RequiresEnrollment { get; set; }

    /// <summary>JWT de propósito Enrollment. No es un token de acceso: no abre ningún endpoint de negocio.</summary>
    public string? EnrollmentToken { get; set; }

    /// <summary>Canal por el que se envió el código, para que la interfaz sepa qué decir.</summary>
    public TwoFactorChannel? Channel { get; set; }

    /// <summary>Destino enmascarado: correo o teléfono según el canal. Vacío para Authenticator.</summary>
    public string? MaskedTarget { get; set; }
}
