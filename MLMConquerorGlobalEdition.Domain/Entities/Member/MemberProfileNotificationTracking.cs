using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Member;

public class MemberProfileNotificationTracking : AuditChangesLongKey
{
    public string MemberId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? FirebaseMessageId { get; set; }
    public bool IsDelivered { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}
