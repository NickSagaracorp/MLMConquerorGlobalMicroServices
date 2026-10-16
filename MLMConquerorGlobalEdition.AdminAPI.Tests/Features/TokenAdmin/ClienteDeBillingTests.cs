using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using MLMConquerorGlobalEdition.AdminAPI.Services;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.TokenAdmin;

/// <summary>
/// El cliente con el que AdminAPI reenvía a Billing: misma clave y mismas reglas que el del BizCenter
/// (Services:BillingBaseUrl, barra final, 90 s, sin aflojar el certificado) y la URL fuera del
/// appsettings.json base, para que la guarda de arranque funcione fuera de Development.
/// </summary>
public class ClienteDeBillingTests
{
    private static ServiceProvider ConCliente(string? baseUrl, string entorno)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Services:BillingBaseUrl"] = baseUrl
        }).Build();
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(entorno);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBillingApiClient(config, env.Object);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Lleva_su_BaseAddress_con_barra_y_90_s()
    {
        using var sp = ConCliente(" https://billing.interno:7443/base ", Environments.Production);

        var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient(BillingApiRegistration.HttpClientName);
        client.BaseAddress.Should().Be(new Uri("https://billing.interno:7443/base/"));
        client.Timeout.Should().Be(TimeSpan.FromSeconds(90));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Fuera_de_Development_sin_BillingBaseUrl_no_arranca(string? valor)
        => ((Action)(() => ConCliente(valor, Environments.Production)))
            .Should().Throw<InvalidOperationException>().WithMessage("*Services:BillingBaseUrl*");

    [Fact]
    public void En_Development_sin_BillingBaseUrl_usa_el_puerto_local_de_Billing()
    {
        using var sp = ConCliente(null, Environments.Development);

        sp.GetRequiredService<IHttpClientFactory>().CreateClient(BillingApiRegistration.HttpClientName)
          .BaseAddress.Should().Be(new Uri("https://localhost:7007/"));
    }

    [Theory]
    [InlineData("MLMConquerorGlobalEdition.AdminAPI")]
    [InlineData("MLMConquerorGlobalEdition.BizCenter")]
    public void El_appsettings_base_no_declara_la_url_de_Billing(string proyecto)
    {
        // Con un valor en el appsettings.json base, la guarda de arranque nunca salta y producción
        // mandaría el token del administrador (o del miembro) a localhost.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MLMConquerorGlobalEdition.slnx"))) dir = dir.Parent;
        dir.Should().NotBeNull();

        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(dir!.FullName, proyecto, "appsettings.json"), optional: false)
            .Build();

        config["Services:BillingBaseUrl"].Should().BeNull();
        config["Services:Billing"].Should().BeNull();
    }
}
