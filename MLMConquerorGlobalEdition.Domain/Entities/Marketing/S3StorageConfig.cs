using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Marketing;

/// <summary>Singleton row (Id = 1). Stores S3 bucket settings configurable from the Admin UI.</summary>
public class S3StorageConfig : AuditChangesIntKey
{
    public string  BucketName    { get; set; } = string.Empty;
    public string  Region        { get; set; } = string.Empty;
    public string? FolderPrefix  { get; set; }  // e.g. "marketing-documents/" — optional
}
