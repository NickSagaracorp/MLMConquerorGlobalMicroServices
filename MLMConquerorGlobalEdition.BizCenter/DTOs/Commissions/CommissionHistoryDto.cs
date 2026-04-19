namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;

public class CommissionHistoryYearDto
{
    public int                              Year        { get; set; }
    public decimal                          TotalIncome { get; set; }
    public List<CommissionHistoryMonthDto>  Months      { get; set; } = new();
}

public class CommissionHistoryMonthDto
{
    public int     MonthNo      { get; set; }
    public string  MonthName    { get; set; } = string.Empty;
    public decimal TotalIncome  { get; set; }
}
