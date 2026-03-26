namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;

public class AdminCommissionDto
{
    public string Id { get; set; } = string.Empty;
    public string BeneficiaryMemberId { get; set; } = string.Empty;
    public string CommissionTypeName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime EarnedDate { get; set; }
    public DateTime? PaymentDate { get; set; }
    public bool IsManualEntry { get; set; }
}
