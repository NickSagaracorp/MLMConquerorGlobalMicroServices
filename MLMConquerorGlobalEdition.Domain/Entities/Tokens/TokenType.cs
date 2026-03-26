using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Tokens;

public class TokenType : AuditChangesIntKey
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsGuestPass { get; set; }
    public string? TemplateUrl { get; set; }
    public bool IsActive { get; set; } = true;
}
