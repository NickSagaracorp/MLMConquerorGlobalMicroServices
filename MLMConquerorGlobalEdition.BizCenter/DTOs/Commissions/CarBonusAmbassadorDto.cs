namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;

public class CarBonusAmbassadorDto
{
    public string  MemberId        { get; set; } = string.Empty;
    public string  AmbassadorName  { get; set; } = string.Empty;
    public decimal TotalPoints     { get; set; }
    public int     EligiblePoints  { get; set; }
}
