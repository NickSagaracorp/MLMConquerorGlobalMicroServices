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

    /// <summary>Extended FSB1 (14-day) window — mirrors the live row so the
    /// archive captures every dimension a future audit / reset routine might
    /// inspect. Required when promos rewrite the live countdown with a new
    /// extended window.</summary>
    public DateTime FastStartBonus1ExtendedStart { get; set; }
    public DateTime FastStartBonus1ExtendedEnd { get; set; }

    public DateTime FastStartBonus2Start { get; set; }
    public DateTime FastStartBonus2End { get; set; }
    public DateTime FastStartBonus3Start { get; set; }
    public DateTime FastStartBonus3End { get; set; }

    /// <summary>
    /// Why the prior countdown was archived. "promo:{promoId}" when written
    /// by the FSB countdown reset job; "rebuild" / "manual" / etc. for other
    /// triggers. Null on legacy rows that predate this column.
    /// </summary>
    public string? Reason { get; set; }

    public virtual MemberCommissionCountDown? CountDown { get; set; }
}
