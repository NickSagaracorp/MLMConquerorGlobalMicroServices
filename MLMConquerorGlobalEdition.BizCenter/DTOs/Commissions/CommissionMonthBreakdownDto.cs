namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;

public class CommissionMonthBreakdownDto
{
    public string                          CommissionTypeName { get; set; } = string.Empty;
    public List<CommissionMonthItemDto>    Items              { get; set; } = new();
}

public class CommissionMonthItemDto
{
    public DateTime  EarnedDate  { get; set; }
    public DateTime  PaymentDate { get; set; }
    public string    Detail      { get; set; } = string.Empty;
    public decimal   Amount      { get; set; }
    public string    Status      { get; set; } = string.Empty;
}
