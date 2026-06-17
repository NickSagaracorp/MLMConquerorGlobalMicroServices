using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Events;

public class CorporatePromo : AuditChangesStringKey
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? BannerUrl { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Multiplier applied to every Sponsor Bonus (Cat 1) earning created by orders that
    /// fall inside [StartDate, EndDate]. 1 = no promo, 2..5 = 2×..5× the configured amount.
    /// Applied at calculation time so admins editing the commission type's <c>Amount</c>
    /// mid-promo don't break payouts. Bounded 1-5 by validation.
    /// </summary>
    public int SponsorBonusMultiplier { get; set; } = 1;

    /// <summary>
    /// Multiplier applied to Builder Bonus (Cat 6 + Cat 7) differential payouts.
    /// 1 = no promo, 2..5 = 2×..5× the tier amount. Independent of SponsorBonusMultiplier
    /// so a promo can boost either or both. Bounded 1-5 by validation.
    /// </summary>
    public int BuilderBonusMultiplier { get; set; } = 1;

    /// <summary>
    /// When true, the admin can trigger a one-shot job that resets the FSB
    /// countdown for every eligible ambassador. The reset moves their current
    /// MemberCommissionCountDown row into history and rewrites it anchored on
    /// the activation moment, giving the cohort a fresh window to earn FSB1.
    /// Eligible = not Terminated AND (countdown already expired
    /// OR within the first 14 days post-signup with no FSB1 earning yet).
    /// </summary>
    public bool ResetFsbCountdown { get; set; }

    /// <summary>
    /// Timestamp of the most recent reset run for this promo. Set the first
    /// time the reset endpoint completes successfully so the same promo can
    /// not accidentally fire the reset twice and hand out double windows.
    /// Null = the reset has never been executed for this promo.
    /// </summary>
    public DateTime? ResetFsbCountdownExecutedAt { get; set; }
}
