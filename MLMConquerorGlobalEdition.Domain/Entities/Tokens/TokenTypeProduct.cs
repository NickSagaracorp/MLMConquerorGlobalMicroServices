using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Tokens;

public class TokenTypeProduct : AuditChangesIntKey
{
    public int TokenTypeId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public int QuantityGranted { get; set; }
}
