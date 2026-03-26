namespace MLMConquerorGlobalEdition.SharedAPICenter.DTOs;

/// <summary>
/// Slim member profile representation exposed to trusted external consumers
/// via the X-Api-Key–protected external API.
/// </summary>
public class ExternalMemberProfileDto
{
    public string MemberId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>String representation of the MemberType enum value.</summary>
    public string MemberType { get; set; } = string.Empty;

    /// <summary>String representation of the MemberAccountStatus enum value.</summary>
    public string Status { get; set; } = string.Empty;
}
