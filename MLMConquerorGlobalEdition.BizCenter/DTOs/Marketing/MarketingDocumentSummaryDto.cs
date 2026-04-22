namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Marketing;

public class MarketingDocumentSummaryDto
{
    public int    Id               { get; set; }
    public string Title            { get; set; } = string.Empty;
    public string DocumentTypeName { get; set; } = string.Empty;
    public string LanguageCode     { get; set; } = string.Empty;
    public string LanguageName     { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public long   FileSizeBytes    { get; set; }
    public string ContentType      { get; set; } = string.Empty;
}
