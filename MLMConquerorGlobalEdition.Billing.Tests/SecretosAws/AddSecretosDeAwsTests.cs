using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using MLMConquerorGlobalEdition.SharedKernel.Server.Hosting;
using Xunit;

namespace MLMConquerorGlobalEdition.Billing.Tests.SecretosAws;

public class AddSecretosDeAwsTests
{
    /// <summary>Lector en memoria que anota qué se le pidió.</summary>
    private sealed class LectorFalso(Dictionary<string, string> secretos) : ILectorDeSecretos
    {
        public List<string> Pedidos { get; } = [];
        public string Leer(string nombre)
        {
            Pedidos.Add(nombre);
            return secretos.TryGetValue(nombre, out var v)
                ? v
                : throw new SecretoNoDisponibleException(nombre, MotivoSecretoNoDisponible.NoExiste);
        }
    }

    private static ConfigurationManager Config(string entorno = "local", bool habilitado = true, string[]? secretos = null)
    {
        var c = new ConfigurationManager();
        var datos = new Dictionary<string, string?>
        {
            ["SecretosAws:Habilitado"] = habilitado.ToString(),
            ["SecretosAws:Entorno"]    = entorno,
            ["SecretosAws:Region"]     = "us-east-1",
            ["ConnectionStrings:DefaultConnection"] = "valor-de-appsettings",
        };
        secretos ??= [];
        for (var i = 0; i < secretos.Length; i++) datos[$"SecretosAws:Secretos:{i}"] = secretos[i];
        c.AddInMemoryCollection(datos);
        return c;
    }

    [Fact]
    public void CargaLasClavesYConvierteDobleGuionBajoEnDosPuntos()
    {
        var c = Config(secretos: ["datos"]);
        var lector = new LectorFalso(new() {
            ["mlmconqueror/local/datos"] = "{\"ConnectionStrings__DefaultConnection\":\"de-aws\",\"ConnectionStrings__Redis\":\"r\"}" });

        c.AddSecretosDeAws(lector, TextWriter.Null);

        c["ConnectionStrings:DefaultConnection"].Should().Be("de-aws");
        c["ConnectionStrings:Redis"].Should().Be("r");
    }

    [Fact]
    public void QuedaPorDebajoDeLasVariablesDeEntorno()
    {
        var prefijo = $"MLMPRUEBA_{Guid.NewGuid():N}_";
        Environment.SetEnvironmentVariable(prefijo + "ConnectionStrings__DefaultConnection", "de-entorno");
        try
        {
            var c = Config(secretos: ["datos"]);
            ((IConfigurationBuilder)c).Sources.Add(new EnvironmentVariablesConfigurationSource { Prefix = prefijo });
            var lector = new LectorFalso(new() {
                ["mlmconqueror/local/datos"] = "{\"ConnectionStrings__DefaultConnection\":\"de-aws\"}" });

            c.AddSecretosDeAws(lector, TextWriter.Null);

            c["ConnectionStrings:DefaultConnection"].Should().Be("de-entorno");
        }
        finally { Environment.SetEnvironmentVariable(prefijo + "ConnectionStrings__DefaultConnection", null); }
    }

    [Fact]
    public void SoloPideLosDeSuEntorno()
    {
        var c = Config(entorno: "local", secretos: ["datos", "twilio"]);
        var lector = new LectorFalso(new() {
            ["mlmconqueror/local/datos"]  = "{}",
            ["mlmconqueror/local/twilio"] = "{}" });

        c.AddSecretosDeAws(lector, TextWriter.Null);

        lector.Pedidos.Should().Equal("mlmconqueror/local/datos", "mlmconqueror/local/twilio");
    }

    [Fact]
    public void Deshabilitado_NoLlamaAAws()
    {
        var c = Config(habilitado: false, secretos: ["datos"]);
        var lector = new LectorFalso([]);

        c.AddSecretosDeAws(lector, TextWriter.Null);

        lector.Pedidos.Should().BeEmpty();
        c["ConnectionStrings:DefaultConnection"].Should().Be("valor-de-appsettings");
    }

    [Fact]
    public void ListaVacia_NoLlamaAAws()
    {
        var c = Config(secretos: []);
        var lector = new LectorFalso([]);

        c.AddSecretosDeAws(lector, TextWriter.Null);

        lector.Pedidos.Should().BeEmpty();
    }

    [Fact]
    public void SiFaltaUnSecreto_NoArranca()
    {
        var c = Config(secretos: ["datos"]);
        var lector = new LectorFalso([]);

        c.Invoking(x => x.AddSecretosDeAws(lector, TextWriter.Null))
            .Should().Throw<SecretoNoDisponibleException>()
            .Where(e => e.Motivo == MotivoSecretoNoDisponible.NoExiste && e.Secreto == "mlmconqueror/local/datos");
    }

    [Theory]
    [InlineData("no es json")]
    [InlineData("[1,2]")]
    [InlineData("{\"Clave\": 5}")]
    [InlineData("{\"Clave\": {\"anidado\":\"x\"}}")]
    public void SecretoQueNoEsJsonDeTextos_FormatoInvalido(string valor)
    {
        var c = Config(secretos: ["datos"]);
        var lector = new LectorFalso(new() { ["mlmconqueror/local/datos"] = valor });

        c.Invoking(x => x.AddSecretosDeAws(lector, TextWriter.Null))
            .Should().Throw<SecretoNoDisponibleException>()
            .Where(e => e.Motivo == MotivoSecretoNoDisponible.FormatoInvalido);
    }

    [Fact]
    public void EntornoNoValido_NoArranca()
    {
        var c = Config(entorno: "production", secretos: ["datos"]);
        c.Invoking(x => x.AddSecretosDeAws(new LectorFalso([]), TextWriter.Null))
            .Should().Throw<InvalidOperationException>().WithMessage("*Entorno*");
    }

    [Fact]
    public void ElLogTraeNombresYNuncaValores()
    {
        var c = Config(secretos: ["jwt-firma"]);
        var lector = new LectorFalso(new() {
            ["mlmconqueror/local/jwt-firma"] = "{\"Jwt__PrivateKeyBase64\":\"MIIEvSECRETO\"}" });
        var log = new StringWriter();

        c.AddSecretosDeAws(lector, log);

        log.ToString().Should().Contain("mlmconqueror/local/jwt-firma").And.Contain("1 clave");
        log.ToString().Should().NotContain("MIIEvSECRETO").And.NotContain("Jwt__PrivateKeyBase64");
    }
}
