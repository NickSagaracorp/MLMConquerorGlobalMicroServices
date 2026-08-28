using System.Security.Cryptography;
using System.Text;

namespace MLMConquerorGlobalEdition.SharedKernel.Configuration;

/// <summary>
/// Valida la configuración de llaves JWT al construir los servicios que firman tokens.
///
/// Rechaza dos casos: la llave ausente, y la llave que estuvo commiteada en
/// appsettings.json hasta 2026-08-27. Esa llave sigue en el historial de git y debe
/// considerarse comprometida de forma permanente; el rechazo por huella impide que
/// alguien la restaure desde un commit viejo y arranque el servicio con ella.
/// </summary>
public static class JwtKeyGuard
{
    /// <summary>
    /// SHA-256 en hexadecimal de la llave privada revocada.
    /// Se guarda la huella, no la llave: la huella no sirve para firmar nada.
    /// </summary>
    private const string RevokedPrivateKeyFingerprint =
        "2ddf53d71674a46e97fcfcb513a5b804aed7eb9f6df3a43ee72e03f4789f0fe5";

    /// <summary>
    /// Devuelve la llave si es utilizable; si no, lanza con un mensaje accionable.
    /// </summary>
    /// <param name="base64">Valor leído de configuración.</param>
    /// <param name="configKey">Clave de configuración, para el mensaje de error.</param>
    public static string ValidatePrivateKey(string? base64, string configKey = "Jwt:PrivateKeyBase64")
    {
        if (string.IsNullOrWhiteSpace(base64))
            throw new InvalidOperationException(
                $"{configKey} no está configurada. En desarrollo va en appsettings.Development.json; " +
                "en producción, en appsettings.Production.json. " +
                "Plantilla: docs/deployment/jwt-keys.template.json.");

        if (Fingerprint(base64) == RevokedPrivateKeyFingerprint)
            throw new InvalidOperationException(
                $"{configKey} contiene la llave revocada que estuvo commiteada en el repositorio. " +
                "Esa llave es pública de forma permanente porque sigue en el historial de git. " +
                "Genera un par nuevo: ver docs/deployment/jwt-keys.template.json.");

        return base64;
    }

    /// <summary>SHA-256 en hexadecimal minúscula del valor, ignorando espacios alrededor.</summary>
    public static string Fingerprint(string base64) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(base64.Trim()))).ToLowerInvariant();
}
