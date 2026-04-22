namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;

public class BoostBonusWeekStatsDto
{
    public string WeekLabel            { get; set; } = string.Empty;
    public int    GoldCount            { get; set; }
    public int    GoldTarget           { get; set; }
    public int    PlatinumCount        { get; set; }
    public int    PlatinumTarget       { get; set; }
    public int    GoldPoints           { get; set; }
    public int    GoldPointsTarget     { get; set; }
    public int    PlatinumPoints       { get; set; }
    public int    PlatinumPointsTarget { get; set; }
}
