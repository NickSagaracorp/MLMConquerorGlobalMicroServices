using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MLMConquerorGlobalEdition.AdminAPI.Services;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.CryptoSignups;

/// <summary>
/// El cliente con el que AdminAPI reenvía a SignupAPI el Bearer del administrador (confirmar cripto,
/// Re-check de Entrax, reintentar altas). Antes aceptaba cualquier certificado en todos los entornos
/// y caía a https://localhost:7005 sin configuración. Ahora, como el de Billing: fuera de Development
/// valida el certificado y exige Services:SignupAPI; en Development acepta el certificado de desarrollo.
/// </summary>
public class ClienteDeSignupApiTests
{
    private static ServiceProvider ConCliente(string? baseUrl, string entorno)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Services:SignupAPI"] = baseUrl
        }).Build();
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(entorno);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSignupApiClient(config, env.Object);
        return services.BuildServiceProvider();
    }

    /// <summary>El manejador del fondo de la cadena (el que habla TLS).</summary>
    private static HttpMessageHandler Primario(ServiceProvider sp)
    {
        HttpMessageHandler h = sp.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(SignupApiRegistration.HttpClientName);
        while (h is DelegatingHandler d) h = d.InnerHandler!;
        return h;
    }

    private static bool AceptaCualquierCertificado(HttpMessageHandler h) => h switch
    {
        HttpClientHandler c   => c.ServerCertificateCustomValidationCallback is not null,
        SocketsHttpHandler s  => s.SslOptions.RemoteCertificateValidationCallback is not null,
        _                     => throw new InvalidOperationException($"Manejador inesperado: {h.GetType()}")
    };

    [Fact]
    public void Lleva_el_nombre_que_usan_los_controladores()
        => SignupApiRegistration.HttpClientName.Should().Be("SignupApi");

    [Fact]
    public void Lleva_su_BaseAddress_con_barra_y_un_tiempo_maximo()
    {
        using var sp = ConCliente(" https://signup.interno:7443/base ", Environments.Production);

        var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient(SignupApiRegistration.HttpClientName);
        client.BaseAddress.Should().Be(new Uri("https://signup.interno:7443/base/"));
        client.Timeout.Should().Be(SignupApiRegistration.Timeout);
        SignupApiRegistration.Timeout.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(60));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Fuera_de_Development_sin_SignupAPI_no_arranca(string? valor)
        => ((Action)(() => ConCliente(valor, Environments.Production)))
            .Should().Throw<InvalidOperationException>().WithMessage("*Services:SignupAPI*");

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Fuera_de_Development_valida_el_certificado(string entorno)
    {
        using var sp = ConCliente("https://signup.interno/", entorno);

        AceptaCualquierCertificado(Primario(sp)).Should().BeFalse(
            "por este cliente viaja el Bearer del administrador.");
    }

    [Fact]
    public void En_Development_sin_SignupAPI_usa_el_puerto_local_y_acepta_el_certificado_de_desarrollo()
    {
        using var sp = ConCliente(null, Environments.Development);

        sp.GetRequiredService<IHttpClientFactory>().CreateClient(SignupApiRegistration.HttpClientName)
          .BaseAddress.Should().Be(new Uri("https://localhost:7005/"));
        AceptaCualquierCertificado(Primario(sp)).Should().BeTrue();
    }

    [Fact]
    public void El_appsettings_base_no_declara_la_url_de_SignupAPI()
    {
        // Con un valor en el appsettings.json base la guarda de arranque nunca salta y producción
        // mandaría el token del administrador a localhost.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MLMConquerorGlobalEdition.slnx"))) dir = dir.Parent;
        dir.Should().NotBeNull();

        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(dir!.FullName, "MLMConquerorGlobalEdition.AdminAPI", "appsettings.json"), optional: false)
            .Build();

        config["Services:SignupAPI"].Should().BeNull();
    }

    [Fact]
    public void Program_no_registra_el_cliente_a_mano_ni_desactiva_el_certificado_de_SignupAPI()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MLMConquerorGlobalEdition.slnx"))) dir = dir.Parent;
        var program = File.ReadAllText(Path.Combine(dir!.FullName, "MLMConquerorGlobalEdition.AdminAPI", "Program.cs"));

        program.Should().Contain("AddSignupApiClient(builder.Configuration, builder.Environment)");
        program.Should().NotContain("AddHttpClient(\"SignupApi\"");
    }
}
