namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;

public class CommissionEarningDto
{
    public string Id { get; set; } = string.Empty;
    public string CommissionTypeName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime EarnedDate { get; set; }
    public DateTime PaymentDate { get; set; }
    public DateTime? PeriodDate { get; set; }
}
