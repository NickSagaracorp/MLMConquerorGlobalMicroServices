using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using FluentAssertions;
using MLMConquerorGlobalEdition.SharedKernel.Server.Hosting;
using Moq;
using Xunit;

namespace MLMConquerorGlobalEdition.Billing.Tests.SecretosAws;

public class LectorDeSecretosAwsTests
{
    private const string Nombre = "mlmconqueror/local/datos";

    private static LectorDeSecretosAws ConCliente(Action<Mock<IAmazonSecretsManager>> preparar)
    {
        var cliente = new Mock<IAmazonSecretsManager>();
        preparar(cliente);
        return new LectorDeSecretosAws(cliente.Object);
    }

    private static void Lanza(Mock<IAmazonSecretsManager> c, Exception ex) =>
        c.Setup(x => x.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), It.IsAny<CancellationToken>()))
         .ThrowsAsync(ex);

    [Fact]
    public void DevuelveElTextoDelSecreto()
    {
        var lector = ConCliente(c => c
            .Setup(x => x.GetSecretValueAsync(
                It.Is<GetSecretValueRequest>(r => r.SecretId == Nombre), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = "{\"a\":\"b\"}" }));

        lector.Leer(Nombre).Should().Be("{\"a\":\"b\"}");
    }

    [Fact]
    public void NoExiste()
    {
        var lector = ConCliente(c => Lanza(c, new ResourceNotFoundException("no")));
        lector.Invoking(l => l.Leer(Nombre)).Should().Throw<SecretoNoDisponibleException>()
            .Where(e => e.Motivo == MotivoSecretoNoDisponible.NoExiste && e.Secreto == Nombre);
    }

    [Fact]
    public void SinPermiso()
    {
        var lector = ConCliente(c => Lanza(c, new AmazonSecretsManagerException("denegado")
            { ErrorCode = "AccessDeniedException" }));
        lector.Invoking(l => l.Leer(Nombre)).Should().Throw<SecretoNoDisponibleException>()
            .Where(e => e.Motivo == MotivoSecretoNoDisponible.SinPermiso);
    }

    [Fact]
    public void SinPermisoSobreLaLlaveKms()
    {
        var lector = ConCliente(c => Lanza(c, new DecryptionFailureException("kms")));
        lector.Invoking(l => l.Leer(Nombre)).Should().Throw<SecretoNoDisponibleException>()
            .Where(e => e.Motivo == MotivoSecretoNoDisponible.SinPermiso);
    }

    [Fact]
    public void SinCredencial()
    {
        var lector = ConCliente(c => Lanza(c, new AmazonClientException("Failed to resolve AWS credentials")));
        lector.Invoking(l => l.Leer(Nombre)).Should().Throw<SecretoNoDisponibleException>()
            .Where(e => e.Motivo == MotivoSecretoNoDisponible.SinCredencial);
    }

    [Fact]
    public void SecretoBinario_EsFormatoInvalido()
    {
        var lector = ConCliente(c => c
            .Setup(x => x.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = null }));
        lector.Invoking(l => l.Leer(Nombre)).Should().Throw<SecretoNoDisponibleException>()
            .Where(e => e.Motivo == MotivoSecretoNoDisponible.FormatoInvalido);
    }

    [Fact]
    public void OtroErrorDeAws()
    {
        var lector = ConCliente(c => Lanza(c, new AmazonSecretsManagerException("throttle")
            { ErrorCode = "ThrottlingException" }));
        lector.Invoking(l => l.Leer(Nombre)).Should().Throw<SecretoNoDisponibleException>()
            .Where(e => e.Motivo == MotivoSecretoNoDisponible.Otro);
    }
}
