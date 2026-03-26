namespace MLMConquerorGlobalEdition.RankEngine.DTOs;

public class CertificateGenerationResponse
{
    public string MemberRankHistoryId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string RankName { get; set; } = string.Empty;
    public string CertificateUrl { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
}
