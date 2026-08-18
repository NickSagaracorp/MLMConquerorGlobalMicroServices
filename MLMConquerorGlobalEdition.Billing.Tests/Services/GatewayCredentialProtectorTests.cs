using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using MLMConquerorGlobalEdition.SharedKernel.Services;
using Xunit;
using MLMConquerorGlobalEdition.Repository.Services.Payout;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services;

/// <summary>
/// El punto de estas pruebas es distinguir CIFRADO de MÁSCARA. Una implementación anterior
/// concatenaba "ENC:" al texto plano, lo que pasaba cualquier chequeo de "empieza con ENC:"
/// mientras dejaba el secreto legible en la base.
///
/// Acá el key ring se respalda en disco temporal en vez de la BD: lo que se ejercita es el
/// protector, no dónde se guardan las llaves. La persistencia en DataProtectionKeys y el
/// envoltorio con certificado se configuran en AddGatewayCredentialProtection.
/// </summary>
public class GatewayCredentialProtectorTests : IDisposable
{
    private readonly string _keyRing =
        Path.Combine(Path.GetTempPath(), "mlmc-keyring-" + Guid.NewGuid().ToString("N"));

    private GatewayCredentialProtector New(string? keyRingOverride = null)
    {
        var directory = Directory.CreateDirectory(keyRingOverride ?? _keyRing);
        var provider  = DataProtectionProvider.Create(
            directory,
            b => b.SetApplicationName(GatewayCredentialProtector.ApplicationName));

        return new GatewayCredentialProtector(provider);
    }

    [Fact]
    public void Round_Trips_A_Secret()
    {
        var protector = New();

        var cipher = protector.Encrypt("super-secret-client-id");

        protector.Decrypt(cipher).Should().Be("super-secret-client-id");
    }

    [Fact]
    public void Ciphertext_Does_Not_Contain_The_Plaintext()
    {
        var protector = New();

        var cipher = protector.Encrypt("super-secret-client-id");

        // Con la implementación vieja ("ENC:" + texto) esta aserción fallaba: el secreto
        // estaba ahí, a la vista, en la columna de la base.
        cipher.Should().NotContain("super-secret-client-id");
        cipher.Should().StartWith("ENC:");
    }

    [Fact]
    public void Ciphertext_Carries_The_DataProtection_Header()
    {
        var protector = New();

        var cipher = protector.Encrypt("whatever");

        // "CfDJ8" es el magic number de Data Protection en base64url. Si falta, lo guardado
        // no pasó por el criptógrafo.
        GatewayCredentialProtector.LooksEncrypted(cipher).Should().BeTrue();
    }

    [Fact]
    public void LooksEncrypted_Rejects_A_Prefixed_Plaintext()
        // Exactamente lo que producía la máscara anterior.
        => GatewayCredentialProtector.LooksEncrypted("ENC:my-plain-secret").Should().BeFalse();

    [Fact]
    public void Two_Encryptions_Of_The_Same_Value_Differ()
    {
        var protector = New();

        // Data Protection usa IV aleatorio: dos cifrados del mismo valor no coinciden. Es lo
        // que impide deducir que dos credenciales comparten secreto mirando la base.
        protector.Encrypt("same").Should().NotBe(protector.Encrypt("same"));
    }

    [Fact]
    public void A_Separate_Process_Sharing_The_Key_Ring_Can_Decrypt()
    {
        // El caso real: AdminAPI cifra la credencial y Billing —otro proceso— la descifra.
        // Se simula con dos protectores independientes sobre el mismo key ring.
        var adminApi = New();
        var billing  = New();

        var cipher = adminApi.Encrypt("acct-89469bb7");

        billing.Decrypt(cipher).Should().Be("acct-89469bb7");
    }

    [Fact]
    public void A_Different_Key_Ring_Cannot_Decrypt()
    {
        var adminApi = New();
        var other = Path.Combine(Path.GetTempPath(), "mlmc-keyring-" + Guid.NewGuid().ToString("N"));

        try
        {
            var strangerHost = New(other);
            var cipher = adminApi.Encrypt("acct-89469bb7");

            // Por esto AdminAPI y Billing deben compartir key ring: sin eso Billing levanta
            // pero no puede leer nada de lo que guardó AdminAPI.
            var act = () => strangerHost.Decrypt(cipher);
            act.Should().Throw<Exception>();
        }
        finally
        {
            if (Directory.Exists(other)) Directory.Delete(other, recursive: true);
        }
    }

    [Fact]
    public void Decrypt_Rejects_A_Value_Without_The_Prefix()
    {
        var protector = New();

        var act = () => protector.Decrypt("not-encrypted-at-all");

        act.Should().Throw<InvalidOperationException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_keyRing)) Directory.Delete(_keyRing, recursive: true);
    }
}
