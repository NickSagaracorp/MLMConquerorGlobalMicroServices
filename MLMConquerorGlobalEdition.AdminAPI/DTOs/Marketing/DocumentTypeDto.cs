namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Marketing;

public class DocumentTypeDto
{
    public int     Id          { get; set; }
    public string  Name        { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int     SortOrder   { get; set; }
    public bool    IsActive    { get; set; }
}

public class CreateDocumentTypeRequest
{
    public string  Name        { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int     SortOrder   { get; set; }
}

public class UpdateDocumentTypeRequest
{
    public string  Name        { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int     SortOrder   { get; set; }
    public bool    IsActive    { get; set; }
}
