using System.Security.Cryptography;

namespace MLMConquerorGlobalEdition.SharedKernel.Configuration;

/// <summary>
/// Valida la configuración de llaves JWT al construir los servicios que firman y verifican tokens.
///
/// Rechaza cuatro casos: la llave ausente, un valor que no es una llave RSA válida, una llave
/// RSA por debajo del tamaño mínimo, y la llave (privada o pública) que sigue commiteada hoy en
/// texto plano. La privada está en
/// MLMConquerorGlobalEdition.AdminAPI/appsettings.json y
/// MLMConquerorGlobalEdition.SignupAPI/appsettings.json, que son los servicios que firman
/// tokens. La pública del mismo par está además desplegada en BizCenter, RankEngine y
/// TicketManagementSystem, que la usan para verificar — cinco servicios en total confían en
/// este par. Ambas llaves están en el historial de git y deben considerarse comprometidas de
/// forma permanente; el rechazo por huella impide que alguien las restaure desde un commit
/// viejo, un backup o un secreto de despliegue y arranque el servicio con ellas.
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

    /// <summary>Tamaño mínimo aceptado, en bits. Por debajo de esto RSA es factorizable.</summary>
    private const int MinimumKeySizeBits = 2048;

    private const string TemplateHint = "Plantilla: docs/deployment/jwt-keys.template.json.";

    /// <summary>
    /// Devuelve la llave privada, en su base64 canónico, si es utilizable; si no, lanza con un
    /// mensaje accionable que nombra la clave de configuración.
    /// </summary>
    /// <param name="base64">Valor leído de configuración: RSA en PKCS#8 DER, en base64.</param>
    /// <param name="configKey">Clave de configuración, para el mensaje de error.</param>
    public static string ValidatePrivateKey(string? base64, string configKey = "Jwt:PrivateKeyBase64") =>
        Validate(
            base64,
            configKey,
            der => { var rsa = RSA.Create(); rsa.ImportPkcs8PrivateKey(der, out _); return rsa; },
            der => InvalidPrivateKeyMessage(configKey, der));

    /// <summary>
    /// Devuelve la llave pública, en su base64 canónico, si es utilizable; si no, lanza con un
    /// mensaje accionable que nombra la clave de configuración.
    /// </summary>
    /// <param name="base64">Valor leído de configuración: SubjectPublicKeyInfo DER, en base64.</param>
    /// <param name="configKey">Clave de configuración, para el mensaje de error.</param>
    public static string ValidatePublicKey(string? base64, string configKey = "Jwt:PublicKeyBase64") =>
        Validate(
            base64,
            configKey,
            der => { var rsa = RSA.Create(); rsa.ImportSubjectPublicKeyInfo(der, out _); return rsa; },
            _ => InvalidPublicKeyMessage(configKey));

    private static string Validate(
        string? base64,
        string configKey,
        Func<byte[], RSA> import,
        Func<byte[]?, string> invalidKeyMessage)
    {
        if (string.IsNullOrWhiteSpace(base64))
            throw new InvalidOperationException(
                $"{configKey} no está configurada. En desarrollo va en appsettings.Development.json; " +
                "en producción, en appsettings.Production.json. " + TemplateHint);

        byte[] der;
        try
        {
            der = Convert.FromBase64String(base64.Trim());
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(invalidKeyMessage(null), ex);
        }

        RSA rsa;
        try
        {
            rsa = import(der);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(invalidKeyMessage(der), ex);
        }

        using (rsa)
        {
            if (rsa.KeySize < MinimumKeySizeBits)
                throw new InvalidOperationException(
                    $"{configKey} tiene {rsa.KeySize} bits; el mínimo es {MinimumKeySizeBits}. " +
                    "Genera un par nuevo. " + TemplateHint);

            if (Fingerprint(rsa) == RevokedKeyFingerprint)
                throw new InvalidOperationException(
                    $"{configKey} contiene la llave revocada que sigue commiteada en el repositorio. " +
                    "Ese par es público de forma permanente porque sigue en el historial de git. " +
                    "Genera un par nuevo. " + TemplateHint);

            return Convert.ToBase64String(der);
        }
    }

    private static string InvalidPrivateKeyMessage(string configKey, byte[]? der)
    {
        if (der is not null && LooksLikePkcs1PrivateKey(der))
            return $"{configKey} está en formato PKCS#1 (la llave de origen tendría la cabecera " +
                "'-----BEGIN RSA PRIVATE KEY-----'). Este repositorio usa PKCS#8, que es lo que " +
                "produce 'openssl genpkey'. Conviértela con: " +
                "openssl pkcs8 -topk8 -nocrypt -in llave.pem -out llave-pkcs8.pem, y usa la salida " +
                "en formato DER, base64. " + TemplateHint;

        return $"{configKey} no es una llave RSA válida. Revisa que el valor sea base64 de una " +
            "llave RSA en formato PKCS#8. " + TemplateHint;
    }

    private static string InvalidPublicKeyMessage(string configKey) =>
        $"{configKey} no es una llave RSA válida. Revisa que el valor sea base64 de una " +
        "llave RSA en formato SubjectPublicKeyInfo. " + TemplateHint;

    /// <summary>
    /// True si el DER importa como RSA PKCS#1 (RSAPrivateKey). Se usa solo para dar un mensaje
    /// de error más útil cuando el DER esperado en PKCS#8 en realidad está en PKCS#1; el guarda
    /// no acepta PKCS#1 como formato válido, solo lo detecta para explicar cómo convertirlo.
    /// </summary>
    private static bool LooksLikePkcs1PrivateKey(byte[] der)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(der, out _);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>SHA-256 en hexadecimal minúscula del SubjectPublicKeyInfo de la llave importada.</summary>
    private static string Fingerprint(RSA rsa) =>
        Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
}
