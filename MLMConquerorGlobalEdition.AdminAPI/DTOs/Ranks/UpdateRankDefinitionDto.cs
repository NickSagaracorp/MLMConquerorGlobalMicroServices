namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Ranks;

public class UpdateRankDefinitionDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CertificateTemplateUrl { get; set; }
}
