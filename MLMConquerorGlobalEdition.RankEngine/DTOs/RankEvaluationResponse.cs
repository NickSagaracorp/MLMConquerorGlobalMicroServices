namespace MLMConquerorGlobalEdition.RankEngine.DTOs;

public class RankEvaluationResponse
{
    public string MemberId { get; set; } = string.Empty;
    public bool RankAchieved { get; set; }
    public RankDefinitionResponse? AchievedRank { get; set; }
    public RankDefinitionResponse? PreviousRank { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime EvaluatedAt { get; set; }
}
