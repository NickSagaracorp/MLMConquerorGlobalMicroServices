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

    public bool IsActive { get; set; } = true;
}

public enum AdminFeeKind
{
    Fixed       = 1,
    Percentage  = 2
}
