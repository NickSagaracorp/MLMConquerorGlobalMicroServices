namespace MLMConquerorGlobalEdition.SharedKernel.Interfaces;

public interface IEncryptionService
{
    /// <summary>Returns the ciphertext prefixed with "ENC:" — safe to store in the database.</summary>
    string Encrypt(string plaintext);

    string Decrypt(string ciphertext);
}
