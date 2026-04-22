using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Marketing;

public class MarketingDocument : AuditChangesIntKey
{
    public string Title            { get; set; } = string.Empty;
    public int    DocumentTypeId   { get; set; }
    public string LanguageCode     { get; set; } = string.Empty;  // ISO 639-1: "en","es","pt"
    public string LanguageName     { get; set; } = string.Empty;  // "English","Español","Português"
    public string S3Key            { get; set; } = string.Empty;  // relative key — never expose to clients
    public string OriginalFileName { get; set; } = string.Empty;
    public long   FileSizeBytes    { get; set; }
    public string ContentType      { get; set; } = string.Empty;
    public bool   IsActive         { get; set; } = true;

    public DocumentType? DocumentType { get; set; }
}
