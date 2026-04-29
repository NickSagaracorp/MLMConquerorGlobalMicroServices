using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Wallet;

/// <summary>
/// Audit trail for ANY change on a member's wallet account: creation, account-identifier
/// change, status flip, default-flag flip. Critical for fraud investigation when an
/// ambassador disputes a payout or claims they never received funds.
/// </summary>
public class MemberProfilesWalletHistory : AuditChangesLongKey
{
    public string     WalletId   { get; set; } = string.Empty;
    public string     MemberId   { get; set; } = string.Empty;
    public WalletType WalletType { get; set; }

    /// <summary>What kind of change occurred.</summary>
    public WalletHistoryAction Action { get; set; }

    public WalletStatus OldStatus { get; set; }
    public WalletStatus NewStatus { get; set; }

    /// <summary>Account identifier (email / wallet address / username) BEFORE the change.</summary>
    public string? OldAccountIdentifier { get; set; }
    /// <summary>Account identifier AFTER the change.</summary>
    public string? NewAccountIdentifier { get; set; }

    public bool? OldIsPreferred { get; set; }
    public bool? NewIsPreferred { get; set; }

    public string? ChangeReason { get; set; }
}

public enum WalletHistoryAction
{
    Created        = 1,
    AccountChanged = 2,
    StatusChanged  = 3,
    SetAsDefault   = 4,
    UnsetAsDefault = 5,
    Deleted        = 6
}
