using Microsoft.AspNetCore.Identity;

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
}
