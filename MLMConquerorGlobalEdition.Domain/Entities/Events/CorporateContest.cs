using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Events;

/// <summary>
/// Time-boxed enrollment contest. Every completed VIP / Elite / Turbo signup
/// inside <see cref="StartDate"/>..<see cref="EndDate"/> awards points to
/// the new member's sponsor AND every upline in the sponsor's enrollment
/// chain (genealogy). The widget on the BizCenter dashboard surfaces the
/// top <see cref="TopX"/> leaderboard while the contest is active; admins
/// keep historical contests around for the leaderboard archive.
///
/// Default <see cref="Name"/>, <see cref="Description"/>, and
/// <see cref="BannerUrl"/> are the English fallback when no
/// <see cref="CorporateContestTranslation"/> matches the viewer's UI culture.
/// </summary>
public class CorporateContest : AuditChangesStringKey
{
    public string  Name         { get; set; } = string.Empty;
    public string? Description  { get; set; }
    public DateTime StartDate   { get; set; }
    public DateTime EndDate     { get; set; }

    /// <summary>S3 / CDN URL of the default (English) banner image.</summary>
    public string? BannerUrl    { get; set; }

    /// <summary>External page hosting the multilingual rules (admin-managed).</summary>
    public string? RulesUrl     { get; set; }

    /// <summary>Leaderboard size shown on the BizCenter widget (default 10).</summary>
    public int     TopX         { get; set; } = 10;

    public bool    IsActive     { get; set; } = true;

    /// <summary>
    /// Horizontal anchor (percentage 0..100) of the points-overlay card on
    /// top of the banner image. The widget uses this with translate(-50%,-50%)
    /// so the card's CENTER lands at (X,Y) regardless of the card's size.
    /// Defaults to 75/45 — the position used by the legacy MWR-Life banner
    /// template (white placeholder right-of-center). Plain banners with the
    /// box at the geometric center can be reconfigured to 50/50 from the
    /// admin form.
    /// </summary>
    public int     PointsBoxXPercent { get; set; } = 75;
    public int     PointsBoxYPercent { get; set; } = 45;

    public ICollection<CorporateContestTranslation> Translations { get; set; }
        = new List<CorporateContestTranslation>();
}
