using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Member;

public class MemberStatusHistory : AuditChangesLongKey
{
    public string MemberId { get; set; } = string.Empty;
    public MemberAccountStatus OldStatus { get; set; }
    public MemberAccountStatus NewStatus { get; set; }
    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; }
}
