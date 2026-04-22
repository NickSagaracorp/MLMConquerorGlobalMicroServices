namespace MLMConquerorGlobalEdition.SignupAPI.DTOs;

/// <summary>Phase 1 of the member signup wizard — personal info and membership level only.</summary>
public class MemberSignupRequest
{
    /// <summary>The sponsor's replicate site slug (e.g. "john-doe" from /john-doe signup URL).</summary>
    public string? SponsorReplicateSite { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? State { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    /// <summary>Required only when Country is United States (US).</summary>
    public string? Ssn { get; set; }
    public int MembershipLevelId { get; set; }
}
