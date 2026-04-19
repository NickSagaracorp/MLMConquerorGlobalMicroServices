namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;

/// <summary>Used for Fast Start Bonus and Presidential Bonus summary stats.</summary>
public class CommissionBonusSummaryDto
{
    public int                    Count       { get; set; }
    public decimal                TotalAmount { get; set; }
    public List<FsbWindowDto>?    Windows     { get; set; }
}

public class FsbWindowDto
{
    public int       WindowNumber { get; set; }
    public bool      IsPromo      { get; set; }
    public decimal   Amount       { get; set; }
    public bool      IsCompleted  { get; set; }
    public bool      IsActive     { get; set; }
    public DateTime? StartDate    { get; set; }
    public DateTime? EndDate      { get; set; }
}
