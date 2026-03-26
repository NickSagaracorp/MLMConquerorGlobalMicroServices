namespace MLMConquerorGlobalEdition.CommissionEngine.DTOs;

public class CalculationResultResponse
{
    public string CommissionType { get; set; } = string.Empty;
    public int RecordsCreated { get; set; }
    public decimal TotalAmountCalculated { get; set; }
    public DateTime PeriodDate { get; set; }
    public string? BatchId { get; set; }
    public List<string> SkippedReasons { get; set; } = new();
}
