namespace MLMConquerorGlobalEdition.Domain.Entities.General;

public abstract class AuditChangesStringKey
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime CreationDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime LastUpdateDate { get; set; }
    public string? LastUpdateBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public byte[]? RowVersion { get; set; }
}
