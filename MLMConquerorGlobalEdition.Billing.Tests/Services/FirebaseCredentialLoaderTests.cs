using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Repository.Services;
using Xunit;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services;

public class FirebaseCredentialLoaderTests
{
    private const string Correo = "push@proyecto-prueba.iam.gserviceaccount.com";

    private static string CuentaDeServicioJson()
    {
        using var rsa = RSA.Create(2048);
        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "service_account",
            ["project_id"] = "proyecto-prueba",
            ["private_key_id"] = "k1",
            ["private_key"] = rsa.ExportPkcs8PrivateKeyPem(),
            ["client_email"] = Correo,
            ["client_id"] = "1",
            ["token_uri"] = "https://oauth2.googleapis.com/token",
        });
    }

    private static IConfiguration Config(string clave, string valor) =>
        new ConfigurationBuilder().AddInMemoryCollection([new(clave, valor)]).Build();

    [Fact]
    public void CargaDesdeBase64()
    {
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(CuentaDeServicioJson()));

        var cred = FirebaseCredentialLoader.Load(Config(FirebaseCredentialLoader.JsonBase64Key, b64));

        cred.UnderlyingCredential.Should().BeOfType<ServiceAccountCredential>()
            .Which.Id.Should().Be(Correo);
    }

    [Fact]
    public void CargaDesdeArchivo()
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"firebase-{Guid.NewGuid():N}.json");
        File.WriteAllText(ruta, CuentaDeServicioJson());
        try
        {
            var cred = FirebaseCredentialLoader.Load(Config(FirebaseCredentialLoader.FilePathKey, ruta));
            cred.UnderlyingCredential.Should().BeOfType<ServiceAccountCredential>()
                .Which.Id.Should().Be(Correo);
        }
        finally { File.Delete(ruta); }
    }

    [Fact]
    public void Base64Invalido_DiceQueClaveFalla()
    {
        FluentActions.Invoking(() => FirebaseCredentialLoader.Load(Config(FirebaseCredentialLoader.JsonBase64Key, "%%")))
            .Should().Throw<InvalidOperationException>().WithMessage($"*{FirebaseCredentialLoader.JsonBase64Key}*");
    }
}
