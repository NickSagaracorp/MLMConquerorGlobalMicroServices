namespace MLMConquerorGlobalEdition.SharedKernel;

/// <summary>
/// Canonical event type strings passed to IPushNotificationService.SendAsync.
/// Must match the eventType values persisted in MemberProfileNotificationTracking.
/// </summary>
public static class NotificationEvents
{
    public const string RankAchieved         = "RANK_ACHIEVED";
    public const string CommissionEarned     = "COMMISSION_EARNED";
    public const string TokenReceived        = "TOKEN_RECEIVED";
    public const string TokenUsed            = "TOKEN_USED";
    public const string MembershipRenewal7d  = "MEMBERSHIP_RENEWAL_7D";
    public const string MembershipRenewal3d  = "MEMBERSHIP_RENEWAL_3D";
    public const string MembershipRenewal1d  = "MEMBERSHIP_RENEWAL_1D";
    public const string MembershipPaymentFailed = "MEMBERSHIP_PAYMENT_FAILED";
    public const string TicketStatusChanged  = "TICKET_STATUS_CHANGED";
    public const string PlacementCompleted   = "PLACEMENT_COMPLETED";
    public const string DownlinePlaced       = "DOWNLINE_PLACED";
    public const string DownlineEnrolled     = "DOWNLINE_ENROLLED";
}
