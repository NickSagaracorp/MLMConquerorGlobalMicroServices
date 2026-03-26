namespace MLMConquerorGlobalEdition.Billing.DTOs;

public class MembershipRenewalRequest
{
    public string MemberId { get; set; } = string.Empty;

    /// <summary>Null means renew the currently active subscription.</summary>
    public string? SubscriptionId { get; set; }
}
