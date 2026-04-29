using Microsoft.AspNetCore.DataProtection;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.BizCenter.Services;

public class EncryptionService : IEncryptionService
{
    private const string Prefix = "ENC:";
    private readonly IDataProtector _protector;

    public EncryptionService(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("MLMConqueror.PiiEncryption.v1");

    public string Encrypt(string plaintext)
        => Prefix + _protector.Protect(plaintext);

    public string Decrypt(string ciphertext)
    {
        if (!ciphertext.StartsWith(Prefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Value is not encrypted.");
        return _protector.Unprotect(ciphertext[Prefix.Length..]);
    }
}
