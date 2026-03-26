using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Wallet;

public class MemberProfilesWalletHistory : AuditChangesLongKey
{
    public string WalletId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public WalletType WalletType { get; set; }
    public WalletStatus OldStatus { get; set; }
    public WalletStatus NewStatus { get; set; }
    public string? ChangeReason { get; set; }
}
