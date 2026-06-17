namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos;

public class CorporatePromoDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? BannerUrl { get; set; }
    public bool IsActive { get; set; }
    public int MemberCount { get; set; }
    public DateTime CreationDate { get; set; }

    /// <summary>Multiplier (1-5) applied to Sponsor Bonus for orders inside the promo window. 1 = no boost.</summary>
    public int SponsorBonusMultiplier { get; set; } = 1;

    /// <summary>Multiplier (1-5) applied to Builder Bonus (Cat 6 + Cat 7) for orders inside the window. 1 = no boost.</summary>
    public int BuilderBonusMultiplier { get; set; } = 1;

    /// <summary>Promo allows admin to one-shot reset eligible ambassadors' FSB countdown.</summary>
    public bool ResetFsbCountdown { get; set; }

    /// <summary>Set the first time the FSB reset job runs successfully — null = never run.</summary>
    public DateTime? ResetFsbCountdownExecutedAt { get; set; }
}
