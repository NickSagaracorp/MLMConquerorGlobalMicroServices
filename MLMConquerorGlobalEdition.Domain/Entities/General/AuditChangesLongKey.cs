namespace MLMConquerorGlobalEdition.Domain.Entities.General;

public abstract class AuditChangesLongKey
{
    public long Id { get; set; }
    public DateTime CreationDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
