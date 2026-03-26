namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Loyalty;

public class LoyaltyPointsDto
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public decimal PointsEarned { get; set; }
    public bool IsLocked { get; set; }
    public bool MissedPayment { get; set; }
    public int NumberOfSuccessPayments { get; set; }
    public int MonthNo { get; set; }
    public int YearNo { get; set; }
    public DateTime? UnlockedAt { get; set; }
}
