using Microsoft.AspNetCore.DataProtection;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Services;

/// <summary>
/// AES-based encryption backed by ASP.NET Core Data Protection.
/// All ciphertext is stored with an "ENC:" prefix. Keys are persisted to AppDbContext
/// (see Program.cs) under the same application name as Billing, so a secret encrypted
/// here (e.g. a Spreedly access_secret entered via the admin credentials UI) can be
/// decrypted by Billing at charge time.
/// </summary>
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
