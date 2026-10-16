using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// El appsettings.json base va al repositorio: Entrax apagado, sin KeyId ni Secret, y con la regla
/// de rate limit del estado que consulta la pantalla.
/// </summary>
public class EntraxConfiguracionTests
{
    private static IConfiguration Base()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MLMConquerorGlobalEdition.slnx"))) dir = dir.Parent;
        dir.Should().NotBeNull();
        return new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(dir!.FullName, "MLMConquerorGlobalEdition.SignupAPI", "appsettings.json"))
            .Build();
    }

    [Fact]
    public void Los_secretos_de_Entrax_no_estan_en_el_appsettings_base()
    {
        var c = Base();

        c["Entrax:KeyId"].Should().BeNullOrEmpty();
        c["Entrax:Secret"].Should().BeNullOrEmpty();
    }

    [Fact]
    public void Entrax_viene_apagado_y_con_la_url_de_la_documentacion()
    {
        var o = EntraxOptions.FromConfiguration(Base());

        o.Enabled.Should().BeFalse();
        o.UseFake.Should().BeFalse();
        o.BaseUrl.Should().Be(EntraxOptions.DefaultBaseUrl);
        o.EffectiveCountries.Should().Equal("RU");
    }

    [Fact]
    public void El_estado_que_consulta_la_pantalla_tiene_su_propio_limite()
    {
        var reglas = Base().GetSection("IpRateLimiting:GeneralRules").GetChildren();

        reglas.Should().Contain(r => r["Endpoint"] == "GET:/api/v1/signups/*/entrax-status" && r["Limit"] == "30");
    }
}
