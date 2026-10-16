using System.Security.Cryptography;
using System.Text;

namespace MLMConquerorGlobalEdition.Repository.Services.Entrax;

/// <summary>
/// Firma HMAC-SHA256 de Entrax (Cash Register API v1).
///
/// Peticiones: canónica "METHOD\nPATH\nTIMESTAMP\nNONCE\nbase64(sha256(body))", firmada con el
/// secreto y en Base64, en la cabecera Authorization: HMAC-SHA256 KeyId=…,Timestamp=…,Nonce=…,Signature=….
///
/// Callbacks: X-Signature = base64(HMAC-SHA256(secreto, cuerpo crudo)). La documentación muestra en
/// un ejemplo un valor que parece hexadecimal; se aceptan las dos codificaciones, siempre sobre el
/// cuerpo crudo y comparando en tiempo constante. El legado comparaba con == y eso no se repite.
/// </summary>
public static class EntraxSignature
{
    public static string BodyHash(string body)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(body)));

    public static string Canonical(string method, string path, long timestamp, string nonce, string body)
        => $"{method}\n{path}\n{timestamp}\n{nonce}\n{BodyHash(body)}";

    public static string Sign(string canonical, string secret)
        => Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(canonical)));

    public static string AuthorizationHeader(string keyId, long timestamp, string nonce, string signature)
        => $"HMAC-SHA256 KeyId={keyId},Timestamp={timestamp},Nonce={nonce},Signature={signature}";

    public static bool VerifyCallback(ReadOnlySpan<byte> rawBody, string? signatureHeader, string? secret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrEmpty(secret))
            return false;

        var given = Decode(signatureHeader.Trim());
        if (given is null) return false;

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), rawBody);
        return CryptographicOperations.FixedTimeEquals(given, expected);
    }

    /// <summary>64 caracteres hexadecimales o Base64. Cualquier otra cosa, null.</summary>
    private static byte[]? Decode(string value)
    {
        if (value.Length == 64 && value.All(Uri.IsHexDigit))
            return Convert.FromHexString(value);

        var buffer = new byte[value.Length];
        return Convert.TryFromBase64String(value, buffer, out var written) ? buffer[..written] : null;
    }
}
