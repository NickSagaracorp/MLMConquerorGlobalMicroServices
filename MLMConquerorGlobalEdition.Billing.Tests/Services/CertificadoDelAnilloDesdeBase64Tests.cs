using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Repository.Services.Payout;
using Xunit;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services;

/// <summary>
/// En EKS y en Docker el PFX del anillo llega desde Secrets Manager, en base64, y no como archivo.
/// </summary>
public class CertificadoDelAnilloDesdeBase64Tests
{
    private static (string Base64, string Huella) NuevoPfx(string clave)
    {
        using var rsa = RSA.Create(2048);
        var pedido = new CertificateRequest("CN=Prueba anillo", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = pedido.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));
        return (Convert.ToBase64String(cert.Export(X509ContentType.Pfx, clave)), cert.Thumbprint);
    }

    private static IConfiguration Config(params (string, string?)[] pares) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pares.Select(p => new KeyValuePair<string, string?>(p.Item1, p.Item2)))
            .Build();

    [Fact]
    public void CargaElCertificadoDesdeBase64()
    {
        var (b64, huella) = NuevoPfx("clave-1");
        var config = Config(
            (GatewayCredentialProtectionExtensions.CertificateBase64Key, b64),
            (GatewayCredentialProtectionExtensions.CertificatePasswordKey, "clave-1"));

        var cert = GatewayCredentialProtectionExtensions.ResolveCertificate(config, env: null);

        cert.Thumbprint.Should().Be(huella);
        cert.HasPrivateKey.Should().BeTrue();
    }

    [Fact]
    public void Base64GanaSobrePath()
    {
        var (b64, huella) = NuevoPfx("clave-1");
        var config = Config(
            (GatewayCredentialProtectionExtensions.CertificateBase64Key, b64),
            (GatewayCredentialProtectionExtensions.CertificatePasswordKey, "clave-1"),
            (GatewayCredentialProtectionExtensions.CertificatePathKey, "/no/existe.pfx"));

        GatewayCredentialProtectionExtensions.ResolveCertificate(config, env: null)
            .Thumbprint.Should().Be(huella);
    }

    [Fact]
    public void Base64Invalido_DiceQueClaveFalla()
    {
        var config = Config((GatewayCredentialProtectionExtensions.CertificateBase64Key, "%%no-es-base64%%"));

        FluentActions.Invoking(() => GatewayCredentialProtectionExtensions.ResolveCertificate(config, env: null))
            .Should().Throw<InvalidOperationException>()
            .WithMessage($"*{GatewayCredentialProtectionExtensions.CertificateBase64Key}*");
    }

    [Fact]
    public void CargaLosRetiradosDesdeBase64()
    {
        var (b64, huella) = NuevoPfx("vieja");
        var config = Config(
            ("DataProtection:Certificate:Retired:0:Base64", b64),
            ("DataProtection:Certificate:Retired:0:Password", "vieja"));

        GatewayCredentialProtectionExtensions.ResolveRetiredCertificates(config)
            .Select(c => c.Thumbprint).Should().Equal(huella);
    }
}
