namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;

public class CarBonusStatsDto
{
    public decimal TotalPoints     { get; set; }
    public decimal EligiblePoints  { get; set; }
    public int     ProgressPercent { get; set; }
    public int     TeamPointsTarget { get; set; }
    public string  MonthLabel      { get; set; } = string.Empty;
}
