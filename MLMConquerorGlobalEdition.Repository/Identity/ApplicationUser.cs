using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Domain.Entities.Security;

namespace MLMConquerorGlobalEdition.Repository.Identity;

/// <summary>
/// Extended ASP.NET Identity user.
/// Staff users (Admin roles) have MemberProfileId = null.
/// Ambassador/Member users always have MemberProfileId set.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// Links to MemberProfile.Id (the MemberId string) for Ambassador/Member users.
    /// Null for system staff (Admin, CommissionManager, etc.).
    /// </summary>
    public string? MemberProfileId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAt { get; set; }

    /// <summary>Hashed refresh token stored server-side (SHA-256 of the raw token).</summary>
    public string? RefreshToken { get; set; }

    public DateTime? RefreshTokenExpiry { get; set; }

    public DateTime CreationDate { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Canal preferido para los códigos de verificación. Email por defecto:
    /// preserva el comportamiento de los miembros del BizCenter que ya tienen 2FA.</summary>
    public TwoFactorChannel PreferredTwoFactorChannel { get; set; } = TwoFactorChannel.Email;

    public DateTime? TwoFactorEnrolledAt { get; set; }

    /// <summary>Teléfono para SMS, cifrado con IEncryptionService. No se reutiliza
    /// IdentityUser.PhoneNumber porque está en texto plano y aquí es a la vez PII y
    /// factor de autenticación.</summary>
    public string? TwoFactorPhoneEncrypted { get; set; }

    /// <summary>Últimos 4 dígitos, para enmascarar en la interfaz sin desencriptar.</summary>
    public string? TwoFactorPhoneLast4 { get; set; }

    public bool TwoFactorPhoneConfirmed { get; set; }
}
