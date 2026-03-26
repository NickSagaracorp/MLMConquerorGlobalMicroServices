namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;

public class CreateCommissionRequest
{
    public string BeneficiaryMemberId { get; set; } = string.Empty;
    public int CommissionTypeId { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
    public DateTime? PeriodDate { get; set; }
}
