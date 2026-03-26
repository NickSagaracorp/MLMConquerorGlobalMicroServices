namespace MLMConquerorGlobalEdition.Signups.DTOs;

public class SponsorInfoResponse
{
    public string MemberId { get; set; } = string.Empty;

    /// <summary>
    /// The name to display publicly.
    /// Returns BusinessName when the sponsor has ShowBusinessName = true, otherwise FullName.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Always populated — the sponsor's legal full name.</summary>
    public string FullName { get; set; } = string.Empty;

    public string? ReplicateSiteSlug { get; set; }
}
