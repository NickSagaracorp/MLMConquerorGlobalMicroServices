using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Billing.DTOs;

public class ChargeRequest
{
    public string MemberId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public WalletType Gateway { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>Optional: attach charge to an existing order.</summary>
    public string? OrderId { get; set; }
}
