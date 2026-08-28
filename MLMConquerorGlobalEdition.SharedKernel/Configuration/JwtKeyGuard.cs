using System.Security.Cryptography;

namespace MLMConquerorGlobalEdition.SharedKernel.Configuration;

/// <summary>
/// Valida la configuración de llaves JWT al construir los servicios que firman y verifican tokens.
///
/// Rechaza tres casos: la llave ausente, un valor que no es una llave RSA válida, y la llave
/// (privada o pública) que sigue commiteada hoy en texto plano en
/// MLMConquerorGlobalEdition.AdminAPI/appsettings.json y
/// MLMConquerorGlobalEdition.SignupAPI/appsettings.json. Esa llave está en el historial de git
/// y debe considerarse comprometida de forma permanente; el rechazo por huella impide que
/// alguien la restaure desde un commit viejo, un backup o un secreto de despliegue y arranque
/// el servicio con ella.
///
/// La huella se calcula sobre el SubjectPublicKeyInfo (SPKI) derivado de la llave, no sobre el
/// string de configuración: el base64 tolera saltos de línea y espacios internos (como los que
/// introduce un .pem envuelto a 64 columnas, un bloque YAML de Kubernetes o un backup de vault),
/// así que huellear el string crudo deja pasar la misma llave con otra codificación. El SPKI es
/// invariante a eso, y además es el mismo para la llave privada y la pública del par: una sola
/// constante sirve para validar ambas con <see cref="ValidatePrivateKey"/> y
/// <see cref="ValidatePublicKey"/>.
/// </summary>
public static class JwtKeyGuard
{
    /// <summary>
    /// SHA-256 en hexadecimal del SubjectPublicKeyInfo (SPKI) del par revocado.
    /// Se guarda la huella, no la llave: la huella no sirve para firmar ni para verificar nada.
    /// </summary>
    private const string RevokedKeyFingerprint =
        "1ae56a7f1c5062a8045b6986c60048e54438dabfa8f21f78bae7de3fa33f9068";

    /// <summary>
    /// Devuelve la llave privada (recortada) si es utilizable; si no, lanza con un mensaje
    /// accionable que nombra la clave de configuración.
    /// </summary>
    /// <param name="base64">Valor leído de configuración: RSA en PKCS#8 DER, en base64.</param>
    /// <param name="configKey">Clave de configuración, para el mensaje de error.</param>
    public static string ValidatePrivateKey(string? base64, string configKey = "Jwt:PrivateKeyBase64") =>
        Validate(base64, configKey, ImportPkcs8Private);

    /// <summary>
    /// Devuelve la llave pública (recortada) si es utilizable; si no, lanza con un mensaje
    /// accionable que nombra la clave de configuración.
    /// </summary>
    /// <param name="base64">Valor leído de configuración: SubjectPublicKeyInfo DER, en base64.</param>
    /// <param name="configKey">Clave de configuración, para el mensaje de error.</param>
    public static string ValidatePublicKey(string? base64, string configKey = "Jwt:PublicKeyBase64") =>
        Validate(base64, configKey, ImportSubjectPublicKeyInfo);

    private static string Validate(string? base64, string configKey, Action<RSA, byte[]> import)
    {
        if (string.IsNullOrWhiteSpace(base64))
            throw new InvalidOperationException(
                $"{configKey} no está configurada. En desarrollo va en appsettings.Development.json; " +
                "en producción, en appsettings.Production.json. " +
                "Plantilla: docs/deployment/jwt-keys.template.json.");

        var trimmed = base64.Trim();

        using var rsa = RSA.Create();
        try
        {
            import(rsa, Convert.FromBase64String(trimmed));
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            throw new InvalidOperationException(
                $"{configKey} no es una llave RSA válida. Revisa que el valor sea base64 de una " +
                "llave RSA en el formato esperado (PKCS#8 para la privada, SubjectPublicKeyInfo " +
                "para la pública). Plantilla: docs/deployment/jwt-keys.template.json.",
                ex);
        }

        if (Fingerprint(rsa) == RevokedKeyFingerprint)
            throw new InvalidOperationException(
                $"{configKey} contiene la llave revocada que sigue commiteada en el repositorio. " +
                "Ese par es público de forma permanente porque sigue en el historial de git. " +
                "Genera un par nuevo: ver docs/deployment/jwt-keys.template.json.");

        return trimmed;
    }

    private static void ImportPkcs8Private(RSA rsa, byte[] der) => rsa.ImportPkcs8PrivateKey(der, out _);

    private static void ImportSubjectPublicKeyInfo(RSA rsa, byte[] der) => rsa.ImportSubjectPublicKeyInfo(der, out _);

    /// <summary>SHA-256 en hexadecimal minúscula del SubjectPublicKeyInfo de la llave importada.</summary>
    private static string Fingerprint(RSA rsa) =>
        Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
}
