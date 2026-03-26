using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Entities.Member;

namespace MLMConquerorGlobalEdition.Domain.Entities.Commission;

public class MemberCommissionCountDownHistory : AuditChangesLongKey
{
    public string CountDownId { get; set; } = string.Empty;
    public Guid MemberId { get; set; }
    public required virtual MemberProfile Member { get; set; }

    public DateTime FastStartBonus1Start { get; set; }
    public DateTime FastStartBonus1End { get; set; }
    public DateTime FastStartBonus2Start { get; set; }
    public DateTime FastStartBonus2End { get; set; }
    public DateTime FastStartBonus3Start { get; set; }
    public DateTime FastStartBonus3End { get; set; }

    public virtual MemberCommissionCountDown? CountDown { get; set; }
}
