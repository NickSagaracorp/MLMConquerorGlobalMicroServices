using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;

namespace MLMConquerorGlobalEdition.Domain.Entities.Wallet;

public class MemberProfilesWallet : AuditChangesStringKey
{
    public string MemberId { get; set; } = string.Empty;
    public WalletType WalletType { get; set; }
    public WalletStatus Status { get; set; } = WalletStatus.Pending;
    public string? AccountIdentifier { get; set; }
    public string? eWalletPasswordEncrypted { get; set; }
    public bool IsPreferred { get; set; }
    public string? Notes { get; set; }

    public void SetEWalletPassword(string encryptedPassword)
    {
        if (string.IsNullOrWhiteSpace(encryptedPassword) || !encryptedPassword.StartsWith("ENC:", StringComparison.Ordinal))
            throw new WalletPasswordStorageException();

        eWalletPasswordEncrypted = encryptedPassword;
    }
}
