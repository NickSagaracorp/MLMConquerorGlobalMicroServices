namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos;

public class UpdateCorporatePromoRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? BannerUrl { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Sponsor Bonus payout multiplier within the promo window. Valid range 1-5. 1 = no boost.</summary>
    public int SponsorBonusMultiplier { get; set; } = 1;

    /// <summary>Builder Bonus payout multiplier within the promo window. Valid range 1-5. 1 = no boost.</summary>
    public int BuilderBonusMultiplier { get; set; } = 1;

    public bool ResetFsbCountdown  { get; set; }
}
