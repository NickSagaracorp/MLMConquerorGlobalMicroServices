namespace MLMConquerorGlobalEdition.CommissionEngine.DTOs;

public class CommissionEarningResponse
{
    public string Id { get; set; } = string.Empty;
    public string BeneficiaryMemberId { get; set; } = string.Empty;
    public string? SourceMemberId { get; set; }
    public string? SourceOrderId { get; set; }
    public int CommissionTypeId { get; set; }
    public string CommissionTypeName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime EarnedDate { get; set; }
    public DateTime PaymentDate { get; set; }
    public DateTime? PeriodDate { get; set; }
    public bool IsManualEntry { get; set; }
    public string? Notes { get; set; }
}
