namespace MLMConquerorGlobalEdition.RankEngine.DTOs;

public class RankProgressResponse
{
    public string MemberId { get; set; } = string.Empty;
    public RankDefinitionResponse? CurrentRank { get; set; }
    public RankDefinitionResponse? NextRank { get; set; }
    public RankMetricsResponse CurrentMetrics { get; set; } = new();
    public RankThresholdProgress? ProgressToNextRank { get; set; }
    public DateTime EvaluatedAt { get; set; }
}
