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

    // ── Selectores de integración ──────────────────────────────────────────
    // Sólo viajan por la AdminAPI. El DTO que ve el ambassador es otro y no los incluye.

    /// <summary>"V1" | "V2" para gateways versionados (PayQuicker). Null si el proveedor no versiona.</summary>
    public string? ApiVersion { get; set; }

    /// <summary>"Sandbox" | "Production" | "Test". Junto con ApiVersion elige la ApiCredential.</summary>
    public string? Environment { get; set; }

    /// <summary>Portal administrativo del proveedor, para el link directo desde el admin.</summary>
    public string? AdminPortalUrl { get; set; }
}
