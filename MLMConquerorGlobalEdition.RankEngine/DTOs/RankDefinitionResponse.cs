namespace MLMConquerorGlobalEdition.RankEngine.DTOs;

public class RankDefinitionResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CertificateTemplateUrl { get; set; }
    public List<RankRequirementResponse> Requirements { get; set; } = new();
}
