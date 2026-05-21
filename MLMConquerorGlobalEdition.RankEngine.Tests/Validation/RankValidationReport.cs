namespace MLMConquerorGlobalEdition.RankEngine.Tests.Validation;

public sealed class RankValidationRow
{
    public int RankDefinitionId { get; init; }
    public string RankName { get; init; } = string.Empty;
    public bool Reached { get; init; }
    public int AchievedRankId { get; init; }
}

public sealed class RankValidationReport
{
    public List<RankValidationRow> Rows { get; } = new();
    public bool AllPassed => Rows.Count == 19 && Rows.TrueForAll(r => r.Reached);

    public string ToMarkdown()
    {
        var lines = new List<string>
        {
            "# Rank Validation Report",
            "",
            $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC",
            $"Result: {(AllPassed ? "PASS — all 19 ranks reachable" : "FAIL")}",
            "",
            "| Rank Id | Rank | Reached | Achieved Rank Id |",
            "|---|---|---|---|"
        };
        foreach (var r in Rows)
            lines.Add($"| {r.RankDefinitionId} | {r.RankName} | {(r.Reached ? "YES" : "NO")} | {r.AchievedRankId} |");
        return string.Join("\n", lines);
    }
}
