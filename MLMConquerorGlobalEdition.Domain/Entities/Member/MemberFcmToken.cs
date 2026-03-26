using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Member;

/// <summary>
/// Stores Firebase Cloud Messaging device registration tokens per member device.
/// </summary>
public class MemberFcmToken : AuditChangesLongKey
{
    public string MemberId  { get; set; } = string.Empty;
    public string Token     { get; set; } = string.Empty;
    public string DeviceId  { get; set; } = string.Empty;
    public string Platform  { get; set; } = string.Empty; // ios | android | web
    public bool   IsActive  { get; set; } = true;
    public DateTime? LastUsedAt { get; set; }
}
