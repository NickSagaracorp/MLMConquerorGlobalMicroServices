namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Ranks;

public class CreateRankDefinitionDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public string? CertificateTemplateUrl { get; set; }
}
