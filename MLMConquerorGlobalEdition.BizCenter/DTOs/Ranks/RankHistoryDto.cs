namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Ranks;

public class RankHistoryDto
{
    public string Id { get; set; } = string.Empty;
    public string RankName { get; set; } = string.Empty;
    public DateTime AchievedAt { get; set; }
    public int? PreviousRankId { get; set; }
    public string? CertificateUrl { get; set; }
}
