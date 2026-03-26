namespace MLMConquerorGlobalEdition.Billing.DTOs;

public class PayoutRequest
{
    public string MemberId { get; set; } = string.Empty;

    /// <summary>Null means pay out all pending earnings due today.</summary>
    public decimal? MaxAmount { get; set; }
}
