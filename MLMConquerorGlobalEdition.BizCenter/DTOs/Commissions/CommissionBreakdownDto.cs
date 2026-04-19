namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;

public class CommissionBreakdownDto
{
    public string  CommissionTypeName { get; set; } = string.Empty;
    public string  Detail             { get; set; } = string.Empty;
    public decimal Amount             { get; set; }
}
