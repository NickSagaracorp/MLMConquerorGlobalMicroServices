namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Tokens;

public class UpdateTokenTypeDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsGuestPass { get; set; }
    public string? TemplateUrl { get; set; }
    public bool IsActive { get; set; }
}
