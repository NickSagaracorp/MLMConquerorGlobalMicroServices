namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos;

public class PromoStatsDto
{
    public int TotalSignups { get; set; }
    public int ActiveSubscriptions { get; set; }
    public int CancelledSubscriptions { get; set; }
    public decimal RetentionRate { get; set; }
    public decimal ChurnRate { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalCommissions { get; set; }
    public Dictionary<string, int> SignupsByMembershipLevel { get; set; } = new();
}
