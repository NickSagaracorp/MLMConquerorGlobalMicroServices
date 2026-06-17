namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Ranks;

public class RankSeniorityRowDto
{
    public string MemberId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int RankDefinitionId { get; set; }
    public string RankName { get; set; } = string.Empty;
    public int ConsecutiveDays { get; set; }
    public DateTime StreakStartDate { get; set; }
    public DateTime StreakEndDate { get; set; }
    public decimal BonusAmount { get; set; }
}
