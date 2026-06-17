using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;

/// <summary>
/// Admin view of a payout gateway (one row per <see cref="WalletType"/>) sourced from
/// the single PaymentGatewayInfo catalog: display, per-gateway admin fee, the minimum
/// pending amount required to become a payout candidate, and the active flag.
/// </summary>
public class PayoutGatewayDto
{
    public int Id { get; set; }
    public WalletType WalletType { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal AdminFee { get; set; }
    public AdminFeeKind AdminFeeKind { get; set; }
    public decimal? MinAdminFee { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal MinimumPayoutAmount { get; set; }
    public bool IsActive { get; set; }
}
