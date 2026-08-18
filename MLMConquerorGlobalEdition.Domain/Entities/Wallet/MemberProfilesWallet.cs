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

    /// <summary>
    /// Cuando se creó la cuenta del lado del PROVEEDOR. Null = todavía no existe.
    ///
    /// Es distinto de Status: Status dice si el método de cobro del miembro es válido y
    /// seleccionable; esto dice si ya hay una cuenta abierta en i-Payout / PayQuicker.
    ///
    /// Importa por plata: en i-Payout cada cuenta le cuesta dinero a la compañía, así que
    /// las wallets que el signup siembra por defecto NO abren cuenta hasta que el ambassador
    /// gana su primera comisión. Este campo es lo que evita que el job de alta diferida
    /// vuelva a crear una cuenta que ya existe.
    /// </summary>
    public DateTime? ProviderAccountCreatedAt { get; set; }

    public void SetEWalletPassword(string encryptedPassword)
    {
        if (string.IsNullOrWhiteSpace(encryptedPassword) || !encryptedPassword.StartsWith("ENC:", StringComparison.Ordinal))
            throw new WalletPasswordStorageException();

        eWalletPasswordEncrypted = encryptedPassword;
    }
}
