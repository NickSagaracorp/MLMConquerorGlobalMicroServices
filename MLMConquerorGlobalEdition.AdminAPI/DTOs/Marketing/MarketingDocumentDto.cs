namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Marketing;

public class MarketingDocumentDto
{
    public int     Id               { get; set; }
    public string  Title            { get; set; } = string.Empty;
    public int     DocumentTypeId   { get; set; }
    public string  DocumentTypeName { get; set; } = string.Empty;
    public string  LanguageCode     { get; set; } = string.Empty;
    public string  LanguageName     { get; set; } = string.Empty;
    public string  OriginalFileName { get; set; } = string.Empty;
    public long    FileSizeBytes    { get; set; }
    public string  ContentType      { get; set; } = string.Empty;
    public bool    IsActive         { get; set; }
    public DateTime CreationDate    { get; set; }
}

public class UpdateMarketingDocumentRequest
{
    public string Title        { get; set; } = string.Empty;
    public int    DocumentTypeId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string LanguageName { get; set; } = string.Empty;
    public bool   IsActive     { get; set; }
}
