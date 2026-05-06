namespace MLMConquerorGlobalEdition.SignupAPI.DTOs;

/// <summary>Request body for real-time token validation on the join page.</summary>
public class ValidateTokenRequest
{
    /// <summary>The token code typed by the user (e.g., "X4P2A9N").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Sponsor's replicate-site slug or MemberId — same value used elsewhere on the join page.</summary>
    public string SponsorReplicateSite { get; set; } = string.Empty;

    /// <summary>Product IDs the user currently has selected. Pass [] when the user hasn't picked yet.</summary>
    public List<string> SelectedProductIds { get; set; } = new();

    /// <summary>FingerprintJS visitorId for fraud telemetry. Optional.</summary>
    public string? VisitorId { get; set; }
}
