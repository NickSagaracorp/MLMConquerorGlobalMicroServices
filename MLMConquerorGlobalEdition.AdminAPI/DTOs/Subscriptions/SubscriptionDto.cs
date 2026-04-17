namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Subscriptions;

/// <summary>
/// Read model returned by GET /api/v1/admin/subscriptions.
/// </summary>
public class SubscriptionDto
{
    public string Id { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public int MembershipLevelId { get; set; }
    public string MembershipLevelName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public bool IsAutoRenew { get; set; }
    public bool IsFree { get; set; }
    public DateTime CreationDate { get; set; }
}
