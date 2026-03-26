using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Wallet;

public class UpdateWalletRequest
{
    public WalletType WalletType { get; set; }
    public bool IsPreferred { get; set; }
    public string? AccountIdentifier { get; set; }
}
