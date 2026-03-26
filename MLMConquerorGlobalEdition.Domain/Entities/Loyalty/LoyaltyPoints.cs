using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Loyalty;

public class LoyaltyPoints : AuditChangesStringKey
{
    public string MemberId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public decimal PointsEarned { get; set; }
    public bool IsLocked { get; set; } = true;
    public bool MissedPayment { get; set; }
    public int NumberOfSuccessPayments { get; set; }
    public int MonthNo { get; set; }
    public int YearNo { get; set; }
    public DateTime? UnlockedAt { get; set; }
}
