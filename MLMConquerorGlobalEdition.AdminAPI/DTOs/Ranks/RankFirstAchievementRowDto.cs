namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Ranks;

public class RankFirstAchievementRowDto
{
    public string MemberId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int RankDefinitionId { get; set; }
    public string RankName { get; set; } = string.Empty;
    public int RankSortOrder { get; set; }
    public DateTime AchievedAt { get; set; }
    public string? PreviousRankName { get; set; }
}
