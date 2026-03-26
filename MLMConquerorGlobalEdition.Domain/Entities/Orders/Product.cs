using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;

namespace MLMConquerorGlobalEdition.Domain.Entities.Orders;

public class Product : AuditChangesStringKey
{
    public required string Name { get; set; } = string.Empty;
    public required string Description { get; set; } = string.Empty;
    public required string ImageUrl { get; set; } = string.Empty;

    // Pricing
    public required decimal MonthlyFee { get; set; } = 0;
    public required decimal SetupFee { get; set; } = 0;
    public decimal Price90Days { get; set; } = 0;
    public decimal Price180Days { get; set; } = 0;
    public decimal AnnualPrice { get; set; } = 0;

    // Promo pricing
    public string? DescriptionPromo { get; set; }
    public decimal MonthlyFeePromo { get; set; } = 0;
    public decimal SetupFeePromo { get; set; } = 0;
    public string? ImageUrlPromo { get; set; }
    public int QualificationPoinsPromo { get; set; } = 0;

    // Qualification & status
    public int QualificationPoins { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    // Join page flags
    /// <summary>Marks this product as the corporate enrollment fee shown in the Enrollment section of the join page.</summary>
    public bool CorporateFee { get; set; } = false;
    /// <summary>Marks this product as selectable in the "Select your membership" section of the join page.</summary>
    public bool JoinPageMembership { get; set; } = false;

    // Migration reference
    public int OldSystemProductId { get; set; } = 0;

    // Membership link
    public int? MembershipLevelId { get; set; }
    public virtual MembershipLevel? MembershipLevel { get; set; }

    public ICollection<ProductCommission> ProductCommissions { get; set; } = new List<ProductCommission>();
}
