using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Authn.Services;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.Authn.Tests.Services;

/// <summary>
/// LA RESTRICCIÓN DE SOLO LECTURA VA FIRMADA DENTRO DEL TOKEN.
/// </summary>
/// <remarks>
/// Es el eslabón de en medio: el manejador de suplantación decide, el emisor lo escribe aquí, y el
/// middleware de cada servicio lo aplica. Si el claim no sale del emisor, las otras dos piezas
/// funcionan y no protegen nada — por eso esto se prueba contra el JWT de verdad y no contra un
/// doble.
/// </remarks>
public class SuplantacionEnElTokenTests
{
    [Fact]
    public void CuandoLaSuplantacionEsDeSoloLectura_ElTokenLoDice()
    {
        var token = Leer(Emisor().GenerateAccessToken(
            userId:                "user-001",
            memberId:              "AMB-001",
            email:                 "amb@test.com",
            roles:                 ["Ambassador"],
            isImpersonating:       true,
            impersonatedBy:        "admin-001",
            impersonationReadOnly: true));

        token.Claims.Should().Contain(c =>
            c.Type == ImpersonationScope.ReadOnlyClaim &&
            c.Value == ImpersonationScope.ReadOnlyValue);
    }

    /// <summary>
    /// Y no lo dice cuando no lo es. Un <c>false</c> explícito en todos los demás tokens invitaría a
    /// leerlo como "restricción evaluada", que no es lo mismo que "no es un token restringido"; y
    /// dejaría un claim que alguien podría acabar comparando mal.
    /// </summary>
    [Fact]
    public void CuandoLaSuplantacionEsCompleta_ElTokenNoLlevaElClaim()
    {
        var token = Leer(Emisor().GenerateAccessToken(
            userId:          "user-001",
            memberId:        "AMB-001",
            email:           "amb@test.com",
            roles:           ["Ambassador"],
            isImpersonating: true,
            impersonatedBy:  "admin-001"));

        token.Claims.Should().NotContain(c => c.Type == ImpersonationScope.ReadOnlyClaim);
    }

    [Fact]
    public void UnTokenDeSesionNormal_NoLlevaElClaim()
    {
        var token = Leer(Emisor().GenerateAccessToken(
            "user-001", "AMB-001", "amb@test.com", ["Ambassador"]));

        token.Claims.Should().NotContain(c => c.Type == ImpersonationScope.ReadOnlyClaim);
    }

    /// <summary>
    /// El puente entre lo que se firma y lo que se lee: el mismo token, leído por el ayudante que
    /// usa el middleware. Si alguien cambia el nombre o el valor del claim en un lado, esto se pone
    /// en rojo aunque las dos pruebas de arriba sigan verdes.
    /// </summary>
    [Fact]
    public void LoQueSeFirma_EsLoQueElServidorLeeComoSoloLectura()
    {
        var restringido = Principal(Emisor().GenerateAccessToken(
            "user-001", "AMB-001", "amb@test.com", ["Ambassador"],
            isImpersonating: true, impersonatedBy: "admin-001", impersonationReadOnly: true));

        var normal = Principal(Emisor().GenerateAccessToken(
            "user-001", "AMB-001", "amb@test.com", ["Ambassador"]));

        ImpersonationScope.IsReadOnly(restringido).Should().BeTrue();
        ImpersonationScope.IsReadOnly(normal).Should().BeFalse();
    }

    private static JwtService Emisor()
    {
        using var rsa = RSA.Create(2048);

        return new JwtService(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:PrivateKeyBase64"]         = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()),
                ["Jwt:Issuer"]                   = "MLMConqueror",
                ["Jwt:Audience"]                 = "MLMConquerorUsers",
                ["Jwt:AccessTokenExpiryMinutes"] = "60"
            })
            .Build());
    }

    private static JwtSecurityToken Leer(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token);

    private static System.Security.Claims.ClaimsPrincipal Principal(string token) =>
        new(new System.Security.Claims.ClaimsIdentity(Leer(token).Claims, "prueba"));
}
