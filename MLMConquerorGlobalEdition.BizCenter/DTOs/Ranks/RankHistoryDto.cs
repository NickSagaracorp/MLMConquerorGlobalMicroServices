namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Ranks;

public class RankHistoryDto
{
    public string   Id               { get; set; } = string.Empty;
    public int      RankDefinitionId { get; set; }
    public string   RankName         { get; set; } = string.Empty;
    public int      RankSortOrder    { get; set; }
    public DateTime AchievedAt       { get; set; }
    public int?     PreviousRankId   { get; set; }
    public string?  PreviousRankName { get; set; }
    /// <summary>True when a certificate has been generated for this achievement.</summary>
    public bool     HasCertificate   { get; set; }
}
