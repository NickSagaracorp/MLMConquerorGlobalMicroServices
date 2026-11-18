using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using MLMConquerorGlobalEdition.Authn.Services;
using Xunit;

namespace MLMConquerorGlobalEdition.Authn.Tests.Services;

/// <summary>
/// El documento JWKS que publica SignupAPI tiene que servir para validar lo que firma la llave
/// privada del par, y no puede llevar nada de la privada.
/// </summary>
public class JwksDocumentTests
{
    [Fact]
    public void ConLaLlaveDelDocumento_SeValidaUnTokenFirmadoConLaPrivadaDelPar()
    {
        using var rsa = RSA.Create(2048);
        var json = JwksDocument.FromPublicKey(Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()));

        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "emisor", audience: "audiencia",
            claims: [new Claim("sub", "alguien")],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256)));

        var keys = new JsonWebKeySet(json).GetSigningKeys();
        var act  = () => new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
        {
            ValidIssuer = "emisor", ValidAudience = "audiencia", IssuerSigningKeys = keys
        }, out _);

        act.Should().NotThrow();
    }

    [Fact]
    public void ElDocumento_SoloLlevaLosCamposPublicos()
    {
        using var rsa = RSA.Create(2048);
        var json = JwksDocument.FromPublicKey(Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()));

        using var doc = JsonDocument.Parse(json);
        var key = doc.RootElement.GetProperty("keys").EnumerateArray().Single();

        key.EnumerateObject().Select(p => p.Name)
            .Should().BeEquivalentTo(["kty", "use", "alg", "kid", "n", "e"]);
        key.GetProperty("kty").GetString().Should().Be("RSA");
        key.GetProperty("use").GetString().Should().Be("sig");
        key.GetProperty("alg").GetString().Should().Be("RS256");
    }

    [Fact]
    public void ElKid_CambiaConLaLlave()
    {
        using var a = RSA.Create(2048);
        using var b = RSA.Create(2048);

        string Kid(RSA rsa) => JsonDocument
            .Parse(JwksDocument.FromPublicKey(Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo())))
            .RootElement.GetProperty("keys")[0].GetProperty("kid").GetString()!;

        Kid(a).Should().NotBe(Kid(b));
        Kid(a).Should().Be(Kid(a));
    }
}
