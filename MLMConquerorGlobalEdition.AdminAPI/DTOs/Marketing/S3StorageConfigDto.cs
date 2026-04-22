namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Marketing;

public class S3StorageConfigDto
{
    public string  BucketName    { get; set; } = string.Empty;
    public string  Region        { get; set; } = string.Empty;
    public string? FolderPrefix  { get; set; }
}
