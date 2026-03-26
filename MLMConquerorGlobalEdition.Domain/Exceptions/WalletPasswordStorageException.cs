namespace MLMConquerorGlobalEdition.Domain.Exceptions;

public class WalletPasswordStorageException : DomainException
{
    public WalletPasswordStorageException()
        : base("WALLET_PASSWORD_NOT_ENCRYPTED", "eWallet password must be stored encrypted. Plain text storage is forbidden.") { }
}
