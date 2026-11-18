using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MLMConquerorGlobalEdition.SharedComponents.Server.Services;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// Un par RSA de pruebas y el validador del portal que confía en él. El portal valida la firma del
/// token de acceso antes de abrir sesión, así que los tokens que simulan la respuesta de SignupAPI
/// tienen que ir firmados con esta llave y la petición tiene que llevar este validador.
/// </summary>
internal static class TokensDePortal
{
    public const string Emisor    = "pruebas";
    public const string Audiencia = "pruebas";

    private static readonly RsaSecurityKey Llave = new(RSA.Create(2048));

    public static readonly PortalAccessTokenValidator Validador = new(Llave, Emisor, Audiencia);

    public static string Firmar(IEnumerable<Claim> claims, DateTime notBefore, DateTime caduca) =>
        FirmarPara(Audiencia, claims, notBefore, caduca);

    /// <summary>Firmado con la llave de pruebas para otra audiencia (por ejemplo, la de los retos).</summary>
    public static string FirmarPara(string audiencia, IEnumerable<Claim> claims, DateTime notBefore, DateTime caduca) =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer:             Emisor,
            audience:           audiencia,
            claims:             claims,
            notBefore:          notBefore,
            expires:            caduca,
            signingCredentials: new SigningCredentials(Llave, SecurityAlgorithms.RsaSha256)));

    public static IServiceCollection ConValidador(this IServiceCollection servicios) =>
        servicios.AddSingleton(Validador);
}
