using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.General;

/// <summary>
/// Per-country default payout gateway. When a new ambassador signs up the
/// signup pipeline reads the row matching their <c>Country.Iso2</c> and seeds
/// a <c>MemberProfilesWallet</c> of that <see cref="WalletType"/> in
/// <c>WalletStatus.Pending</c> — the ambassador can later swap or replace it
/// from the BizCenter Wallet tab.
///
/// Admin-managed (Configuration → Payout Defaults). One row per country
/// enforced via a unique index on <see cref="CountryIso2"/>; rows with
/// <see cref="IsActive"/> = false are kept for audit but skipped at signup.
/// </summary>
public class CountryPayoutDefault : AuditChangesIntKey
{
    /// <summary>ISO-3166 alpha-2 code (matches <see cref="Country.Iso2"/>).</summary>
    public required string CountryIso2 { get; set; }

    public WalletType WalletType { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Optional admin note explaining the choice (e.g., "Dwolla
    /// sandbox not yet onboarded for this country").</summary>
    public string? Notes { get; set; }
}
