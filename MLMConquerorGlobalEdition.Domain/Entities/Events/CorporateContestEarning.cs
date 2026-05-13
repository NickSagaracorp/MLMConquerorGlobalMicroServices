using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Events;

/// <summary>
/// Append-only ledger row recording one upline's contest points credit for
/// one source order. Idempotency is enforced by a unique index on
/// (ContestId, SourceOrderId, BeneficiaryMemberId): a sweep re-run never
/// double-credits the same upline for the same signup.
///
/// The leaderboard is materialized on demand by summing <see cref="Points"/>
/// per <see cref="BeneficiaryMemberId"/> filtered by <see cref="ContestId"/>.
/// Keeping it as a transactional ledger preserves auditability — admin can
/// always answer "exactly which signup gave member X point Y".
/// </summary>
public class CorporateContestEarning : AuditChangesLongKey
{
    public string  ContestId            { get; set; } = string.Empty;
    public string  BeneficiaryMemberId  { get; set; } = string.Empty;
    public string  SourceMemberId       { get; set; } = string.Empty;
    public string  SourceOrderId        { get; set; } = string.Empty;

    /// <summary>0 = direct sponsor, 1 = sponsor's sponsor, …
    /// Captured for analytics; not used in leaderboard math.</summary>
    public int     Level                { get; set; }

    public int     Points               { get; set; }

    /// <summary>Denormalized membership-level id (2/3/4 for VIP/Elite/Turbo)
    /// so the contest report can break points down by tier without re-joining
    /// to OrderDetails after the fact.</summary>
    public int     MembershipLevelId    { get; set; }

    public DateTime EarnedDate          { get; set; }

    public CorporateContest? Contest { get; set; }
}
