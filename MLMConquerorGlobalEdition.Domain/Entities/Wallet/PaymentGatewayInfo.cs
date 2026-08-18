using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Wallet;

/// <summary>
/// Describes a payment gateway the company supports — used to render the
/// rules / process to the ambassador AND to apply the per-gateway admin fee
/// when commissions are paid out. One row per <see cref="WalletType"/>.
/// </summary>
public class PaymentGatewayInfo : AuditChangesIntKey
{
    public WalletType WalletType { get; set; }

    /// <summary>Display name shown to the user (e.g. "eWallet (I-Payout)").</summary>
    public string DisplayName  { get; set; } = string.Empty;

    /// <summary>Long-form rules / process explanation shown in the wallet card.</summary>
    public string Description  { get; set; } = string.Empty;

    /// <summary>Fee charged by the company for each payout via this gateway.</summary>
    public decimal AdminFee { get; set; }

    /// <summary>Whether <see cref="AdminFee"/> is a flat amount or a percentage of the payout.</summary>
    public AdminFeeKind AdminFeeKind { get; set; } = AdminFeeKind.Fixed;

    /// <summary>
    /// Minimum admin fee per transaction in <see cref="Currency"/>. Only meaningful
    /// when <see cref="AdminFeeKind"/> is <see cref="AdminFeeKind.Percentage"/> —
    /// the effective fee is max(AdminFee% × payout, MinAdminFee). Null = no floor.
    /// </summary>
    public decimal? MinAdminFee { get; set; }

    /// <summary>USD/EUR/etc — only meaningful when <see cref="AdminFeeKind"/> is Fixed.</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Minimum amount (USD) a member must have pending before they become a payout
    /// candidate for this gateway. The payout dashboard, CSV batch export and the
    /// orchestrator all gate eligibility on this threshold. Editable by admin.
    /// </summary>
    public decimal MinimumPayoutAmount { get; set; }

    public bool IsActive { get; set; } = true;

    // ── Selectores de integración (admin) ──────────────────────────────────
    // Juntos eligen la fila de ApiCredential que usa el gateway en runtime:
    //   ServiceKey = "{gateway}{ApiVersion}"  +  Environment
    // Son admin-only: NO se exponen en el DTO que ve el ambassador.

    /// <summary>
    /// Versión de la API del proveedor a usar. Sólo aplica a gateways que ofrecen más de
    /// una (hoy PayQuicker: "V1" | "V2"). Null = el proveedor no versiona.
    /// </summary>
    public string? ApiVersion { get; set; }

    /// <summary>
    /// Ambiente contra el que opera este gateway: "Sandbox" | "Production" | "Test".
    /// Debe coincidir con ApiCredential.Environment. Null = hereda el default del servicio.
    /// </summary>
    public string? Environment { get; set; }

    /// <summary>
    /// URL del portal administrativo del proveedor, para el link directo desde el admin.
    /// Las credenciales de ese portal viven cifradas en ApiCredential — nunca acá, porque
    /// esta entidad se serializa hacia el ambassador.
    /// </summary>
    public string? AdminPortalUrl { get; set; }
}

public enum AdminFeeKind
{
    Fixed       = 1,
    Percentage  = 2
}
