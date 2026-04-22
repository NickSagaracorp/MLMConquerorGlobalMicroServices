namespace MLMConquerorGlobalEdition.SignupAPI.DTOs;

/// <summary>Phase 1 of the ambassador signup wizard — personal info and membership level only.</summary>
public class AmbassadorSignupRequest
{
    /// <summary>The sponsor's replicate site slug (e.g. "john-doe" from /john-doe signup URL).</summary>
    public string? SponsorReplicateSite { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }           // must be ≥ 18 years
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }

    public string Country { get; set; } = string.Empty;
    public string? State { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? ZipCode { get; set; }

    /// <summary>Required only when Country is United States (US).</summary>
    public string? Ssn { get; set; }

    public string? BusinessName { get; set; }
    public bool ShowBusinessName { get; set; }

    public string? ReplicateSiteSlug { get; set; }
    public int MembershipLevelId { get; set; }
}
