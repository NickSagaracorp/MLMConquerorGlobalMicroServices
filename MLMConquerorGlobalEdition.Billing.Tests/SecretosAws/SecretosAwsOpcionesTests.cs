using FluentAssertions;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.SharedKernel.Server.Hosting;
using Xunit;

namespace MLMConquerorGlobalEdition.Billing.Tests.SecretosAws;

public class SecretosAwsOpcionesTests
{
    private static IConfiguration Config(params (string Clave, string? Valor)[] pares) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pares.Select(p => new KeyValuePair<string, string?>(p.Clave, p.Valor)))
            .Build();

    [Fact]
    public void LeeHabilitadoEntornoRegionYLista()
    {
        var o = SecretosAwsOpciones.Leer(Config(
            ("SecretosAws:Habilitado", "true"),
            ("SecretosAws:Entorno", "staging"),
            ("SecretosAws:Region", "us-east-1"),
            ("SecretosAws:Secretos:0", "datos"),
            ("SecretosAws:Secretos:1", "jwt-firma")));

        o.Habilitado.Should().BeTrue();
        o.Entorno.Should().Be("staging");
        o.Region.Should().Be("us-east-1");
        o.Secretos.Should().Equal("datos", "jwt-firma");
    }

    [Fact]
    public void SinSeccion_QuedaDeshabilitado()
    {
        SecretosAwsOpciones.Leer(Config()).Habilitado.Should().BeFalse();
    }

    [Theory]
    [InlineData("local")]
    [InlineData("staging")]
    [InlineData("prod")]
    public void ElNombreCompletoLlevaElEntorno(string entorno)
    {
        var o = new SecretosAwsOpciones(true, entorno, "us-east-1", ["datos"]);
        o.Validar();
        o.NombreCompleto("datos").Should().Be($"mlmconqueror/{entorno}/datos");
    }

    [Theory]
    [InlineData("")]
    [InlineData("production")]
    [InlineData("Prod")]
    public void EntornoNoValido_Falla(string entorno)
    {
        var o = new SecretosAwsOpciones(true, entorno, "us-east-1", ["datos"]);
        o.Invoking(x => x.Validar()).Should().Throw<InvalidOperationException>()
            .WithMessage("*Entorno*local, staging o prod*");
    }

    [Fact]
    public void SinRegion_Falla()
    {
        new SecretosAwsOpciones(true, "local", "", ["datos"])
            .Invoking(x => x.Validar()).Should().Throw<InvalidOperationException>()
            .WithMessage("*Region*");
    }

    [Theory]
    [InlineData("../prod/datos")]
    [InlineData("prod/datos")]
    [InlineData("Datos")]
    [InlineData("datos ")]
    public void NombreDeSecretoConCaracteresRaros_Falla(string secreto)
    {
        // Un nombre con "/" podría salirse del entorno: "../prod/datos" desde local.
        new SecretosAwsOpciones(true, "local", "us-east-1", [secreto])
            .Invoking(x => x.Validar()).Should().Throw<InvalidOperationException>()
            .WithMessage("*nombre de secreto*");
    }
}
