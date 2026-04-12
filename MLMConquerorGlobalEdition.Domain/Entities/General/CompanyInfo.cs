namespace MLMConquerorGlobalEdition.Domain.Entities.General;

/// <summary>
/// Singleton row — the platform's MLM company identity.
/// Always Id = 1. Admin can GET and PUT but never POST/DELETE.
/// Used in certificate generation, emails, and public-facing branding.
/// </summary>
public class CompanyInfo : AuditChangesIntKey
{
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyLegalId { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string SupportEmail { get; set; } = string.Empty;
    public string? PresidentName { get; set; }
    public string? WebsiteUrl { get; set; }
    /// <summary>S3 URL of the company logo — optional, used in emails and certificates.</summary>
    public string? LogoUrl { get; set; }
}
