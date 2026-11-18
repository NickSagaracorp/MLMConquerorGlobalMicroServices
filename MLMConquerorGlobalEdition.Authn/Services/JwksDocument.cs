using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using MLMConquerorGlobalEdition.SharedKernel.Configuration;

namespace MLMConquerorGlobalEdition.Authn.Services;

/// <summary>
/// El documento JWKS (RFC 7517) con la llave PÚBLICA con la que se firman los tokens, para que
/// las aplicaciones cliente validen la firma sin llevar la llave incrustada.
/// </summary>
/// <remarks>
/// Solo sale el módulo y el exponente de la llave pública (<c>n</c>, <c>e</c>): nada de esto es
/// secreto. Se arma con <c>Jwt:PublicKeyBase64</c> y no con la privada, para que este código no
/// pueda filtrar la privada ni por error.
///
/// El <c>kid</c> es la huella SHA-256 de la llave: cambia sola al rotarla, sin otra configuración.
/// </remarks>
public static class JwksDocument
{
    public const string Route = "/.well-known/jwks.json";

    /// <summary>El JSON del documento para la llave pública dada (SubjectPublicKeyInfo en base64).</summary>
    public static string FromPublicKey(string? publicKeyBase64)
    {
        var spki = Convert.FromBase64String(JwtKeyGuard.ValidatePublicKey(publicKeyBase64));

        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(spki, out _);
        var p = rsa.ExportParameters(includePrivateParameters: false);

        var key = new Dictionary<string, string>
        {
            ["kty"] = JsonWebAlgorithmsKeyTypes.RSA,
            ["use"] = JsonWebKeyUseNames.Sig,
            ["alg"] = SecurityAlgorithms.RsaSha256,
            ["kid"] = Base64UrlEncoder.Encode(SHA256.HashData(spki)),
            ["n"]   = Base64UrlEncoder.Encode(p.Modulus!),
            ["e"]   = Base64UrlEncoder.Encode(p.Exponent!),
        };

        return JsonSerializer.Serialize(new { keys = new[] { key } });
    }
}
