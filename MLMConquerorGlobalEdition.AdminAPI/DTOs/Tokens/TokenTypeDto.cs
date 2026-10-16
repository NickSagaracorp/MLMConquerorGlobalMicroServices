using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Tokens;

public class TokenTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsGuestPass { get; set; }
    public string? TemplateUrl { get; set; }
    public bool IsActive { get; set; }
    public TokenCategory Category { get; set; }

    /// <summary>Se vende en el BizCenter (Plan F, D1).</summary>
    public bool IsPurchasable { get; set; }
}
