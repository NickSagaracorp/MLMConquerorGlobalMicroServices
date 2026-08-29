using Microsoft.AspNetCore.DataProtection;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SharedKernel.Services;

/// <summary>
/// Cifra y descifra las credenciales de gateway (AES-256-CBC + HMAC-SHA256, vía ASP.NET
/// Core Data Protection). El ciphertext se guarda con prefijo "ENC:".
///
/// Esta clase sólo aplica el protector; QUIÉN guarda las llaves y CÓMO se protegen se
/// configura aparte — ver AddGatewayCredentialProtection en el proyecto Billing.
/// Resumen del esquema, documentado en detalle en el artículo de KB
/// "gateway-credential-encryption":
///
///   secreto  ──cifrado con──▶  llave del key ring  ──envuelta con──▶  certificado X.509
///   (en ApiCredentials)        (tabla DataProtectionKeys)             (fuera de la base)
///
/// Los dos factores viven separados a propósito: un backup de la base no alcanza para
/// descifrar nada sin la clave privada del certificado.
/// </summary>
public sealed class GatewayCredentialProtector : IEncryptionService
{
    /// <summary>
    /// Nombre de aplicación compartido. Es lo que hace que AdminAPI y Billing deriven las
    /// MISMAS llaves. Cambiarlo vuelve ilegible todo lo ya cifrado.
    /// </summary>
    public const string ApplicationName = "MLMConqueror.GatewayCredentials.v1";

    /// <summary>Purpose del protector. Mismo criterio: cambiarlo invalida lo existente.</summary>
    public const string ProtectorPurpose = "MLMConqueror.GatewayCredentials";

    private const string Prefix = "ENC:";

    private readonly IDataProtector _protector;

    public GatewayCredentialProtector(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector(ProtectorPurpose);

    /// <summary>Cifra y devuelve el valor con prefijo, listo para persistir.</summary>
    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return Prefix + _protector.Protect(plaintext);
    }

    public string Decrypt(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext) || !ciphertext.StartsWith(Prefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Value is not encrypted.");

        return _protector.Unprotect(ciphertext[Prefix.Length..]);
    }

    /// <summary>
    /// True si el valor parece ciphertext real de Data Protection y no texto plano con el
    /// prefijo puesto a mano. Data Protection antepone una cabecera de 4 bytes con un magic
    /// number que, en base64url, siempre empieza con "CfDJ8".
    ///
    /// Sirve para detectar credenciales guardadas por implementaciones anteriores que sólo
    /// concatenaban "ENC:" al texto plano sin cifrar nada.
    /// </summary>
    public static bool LooksEncrypted(string? value) =>
        value is not null
        && value.StartsWith(Prefix, StringComparison.Ordinal)
        && value.AsSpan(Prefix.Length).StartsWith("CfDJ8");
}
