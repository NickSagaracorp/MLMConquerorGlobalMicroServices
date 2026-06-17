namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Security;

/// <summary>
/// Admin request to clear fingerprint events that are blocking a visitor from completing signup.
/// At least one of VisitorId or IpAddress must be provided. Reason is required for the audit trail.
/// </summary>
public class UnblockFingerprintRequest
{
    /// <summary>FingerprintJS visitorId to clear. Mutually optional with IpAddress.</summary>
    public string? VisitorId { get; set; }

    /// <summary>IP address to clear (covers cases where visitorId was missing/unstable).</summary>
    public string? IpAddress { get; set; }

    /// <summary>Free-text justification recorded on every cleared row (e.g. "Support call — legitimate user").</summary>
    public string Reason { get; set; } = string.Empty;
}
