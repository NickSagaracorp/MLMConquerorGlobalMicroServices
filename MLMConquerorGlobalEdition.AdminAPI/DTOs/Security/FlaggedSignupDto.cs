namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Security;

/// <summary>Row returned to the admin "flagged signups" page — one per SignupRiskFingerprint event.</summary>
public class FlaggedSignupDto
{
    public long   Id                   { get; set; }
    public string VisitorId            { get; set; } = string.Empty;
    public string Flow                 { get; set; } = string.Empty;
    public string? SponsorReplicateSite { get; set; }
    public string? IpAddress           { get; set; }
    public string? UserAgent           { get; set; }
    public string? OrderId             { get; set; }
    public string? MemberId            { get; set; }
    public bool   IsFlagged            { get; set; }
    public string? FlagReason          { get; set; }
    public bool   Cleared              { get; set; }
    public DateTime? ClearedAt         { get; set; }
    public string? ClearedBy           { get; set; }
    public string? ClearReason         { get; set; }
    public DateTime CreationDate       { get; set; }
}
