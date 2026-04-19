using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Entities.Member;

namespace MLMConquerorGlobalEdition.Domain.Entities.Commission;

public class MemberCommissionCountDown : AuditChangesStringKey
{
    public Guid MemberId { get; set; }
    public required virtual MemberProfile Member { get; set; }

    // Normal Window 1 — 7 days from enrollment
    public DateTime FastStartBonus1Start { get; set; }
    public DateTime FastStartBonus1End { get; set; }

    // Extended Window 1 — 14 days from enrollment (separate promo tracking)
    public DateTime FastStartBonus1ExtendedStart { get; set; }
    public DateTime FastStartBonus1ExtendedEnd { get; set; }

    // Window 2 — 7 days starting after Normal Window 1 ends
    public DateTime FastStartBonus2Start { get; set; }
    public DateTime FastStartBonus2End { get; set; }

    // Window 3 — 7 days starting after Window 2 ends
    public DateTime FastStartBonus3Start { get; set; }
    public DateTime FastStartBonus3End { get; set; }
}
