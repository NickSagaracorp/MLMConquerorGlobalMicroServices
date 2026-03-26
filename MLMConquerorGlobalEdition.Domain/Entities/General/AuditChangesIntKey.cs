namespace MLMConquerorGlobalEdition.Domain.Entities.General;

public abstract class AuditChangesIntKey
{
    public int Id { get; set; }
    public DateTime CreationDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? LastUpdateDate { get; set; }
    public string? LastUpdateBy { get; set; }
}
