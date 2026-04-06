namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Dashboard;

public class DashboardStatsDto
{
    public decimal TotalEarnings { get; set; }
    public int     TeamSize      { get; set; }
    public int     TokenBalance  { get; set; }
    public string? CurrentRank   { get; set; }

    /// <summary>Fast Start Bonus windows (up to 3) for Ambassador dashboard.</summary>
    public IEnumerable<FsbWindowDto> FsbWindows { get; set; } = [];
}

public class FsbWindowDto
{
    public int       WindowNumber { get; set; }
    public DateTime? StartDate    { get; set; }
    public DateTime? EndDate      { get; set; }
    public decimal   Earned       { get; set; }
    public string    Status       { get; set; } = string.Empty;
}
