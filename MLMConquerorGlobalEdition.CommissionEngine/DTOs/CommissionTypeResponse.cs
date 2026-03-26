namespace MLMConquerorGlobalEdition.CommissionEngine.DTOs;

public class CommissionTypeResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CategoryName { get; set; }
    public decimal Percentage { get; set; }
    public int PaymentDelayDays { get; set; }
    public bool IsActive { get; set; }
    public bool IsRealTime { get; set; }
    public bool IsPaidOnSignup { get; set; }
    public bool IsPaidOnRenewal { get; set; }
    public bool ResidualBased { get; set; }
    public double ResidualPercentage { get; set; }
    public int LevelNo { get; set; }
    public int PersonalPoints { get; set; }
    public int TeamPoints { get; set; }
}
