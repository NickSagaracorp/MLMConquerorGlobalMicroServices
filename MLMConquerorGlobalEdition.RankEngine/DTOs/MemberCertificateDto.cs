namespace MLMConquerorGlobalEdition.RankEngine.DTOs;

/// <summary>One certificate-eligible rank a member has achieved, with its certificate status.</summary>
public class MemberCertificateDto
{
    public string   MemberRankHistoryId { get; set; } = string.Empty;
    public int      RankDefinitionId    { get; set; }
    public string   RankName            { get; set; } = string.Empty;
    public int      SortOrder           { get; set; }
    public DateTime FirstAchievedAt     { get; set; }
    public string?  CertificateUrl      { get; set; }
    public bool     HasCertificate      { get; set; }
}
