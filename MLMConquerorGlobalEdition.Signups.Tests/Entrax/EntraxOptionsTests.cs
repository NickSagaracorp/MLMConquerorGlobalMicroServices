using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

public class EntraxOptionsTests
{
    private static EntraxOptions Completas() => new()
    {
        Enabled = true, KeyId = "key-1", Secret = "s3cr3t",
        CallbackUrl = "https://alta.ejemplo.com/api/v1/signups/entrax/callback"
    };

    [Fact]
    public void Apagado_no_exige_nada_ni_en_produccion()
        => EntraxOptionsGuard.Problems(new EntraxOptions(), isDevelopment: false).Should().BeEmpty();

    [Fact]
    public void Encendido_en_produccion_sin_credenciales_ni_callback_no_arranca()
    {
        var o = new EntraxOptions { Enabled = true };

        var act = () => EntraxOptionsGuard.ThrowIfInvalid(o, isDevelopment: false);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*KeyId*").And.Message.Should().Contain("Secret").And.Contain("CallbackUrl");
    }

    [Theory]
    [InlineData("http://alta.ejemplo.com/cb")]
    [InlineData("/api/v1/signups/entrax/callback")]
    [InlineData("")]
    public void El_callback_tiene_que_ser_https_absoluto(string url)
    {
        var o = Completas();
        o.CallbackUrl = url;

        EntraxOptionsGuard.Problems(o, isDevelopment: false).Should().ContainSingle(p => p.Contains("CallbackUrl"));
    }

    [Fact]
    public void Encendido_y_completo_arranca()
        => ((Action)(() => EntraxOptionsGuard.ThrowIfInvalid(Completas(), isDevelopment: false))).Should().NotThrow();

    [Fact]
    public void El_fake_fuera_de_Development_no_arranca_aunque_este_apagado()
    {
        var act = () => EntraxOptionsGuard.ThrowIfInvalid(new EntraxOptions { UseFake = true }, isDevelopment: false);
        act.Should().Throw<InvalidOperationException>().WithMessage("*UseFake*");
    }

    [Fact]
    public void En_Development_el_fake_no_necesita_credenciales()
        => EntraxOptionsGuard.Problems(new EntraxOptions { Enabled = true, UseFake = true }, isDevelopment: true)
            .Should().BeEmpty();

    [Fact]
    public void En_Development_avisa_pero_no_tumba_el_arranque()
    {
        var o = new EntraxOptions { Enabled = true };

        EntraxOptionsGuard.Problems(o, isDevelopment: true).Should().NotBeEmpty();
        ((Action)(() => EntraxOptionsGuard.ThrowIfInvalid(o, isDevelopment: true))).Should().NotThrow();
    }

    [Fact]
    public void Por_defecto_solo_se_ofrece_en_Rusia()
    {
        var o = new EntraxOptions { Enabled = true };

        o.IsOfferedFor("RU").Should().BeTrue();
        o.IsOfferedFor("ru").Should().BeTrue();
        o.IsOfferedFor("US").Should().BeFalse();
        o.IsOfferedFor(null).Should().BeFalse();
    }

    [Fact]
    public void Apagado_no_se_ofrece_en_ningun_pais()
        => new EntraxOptions { Enabled = false }.IsOfferedFor("RU").Should().BeFalse();

    [Fact]
    public void Los_paises_de_la_configuracion_sustituyen_a_los_de_por_defecto()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Entrax:Enabled"]      = "true",
            ["Entrax:Countries:0"]  = " kz ",
            ["Entrax:Countries:1"]  = "BY"
        }).Build();

        var o = EntraxOptions.FromConfiguration(config);

        o.EffectiveCountries.Should().Equal("KZ", "BY");
        o.IsOfferedFor("RU").Should().BeFalse();
    }

    [Fact]
    public void La_tolerancia_del_callback_nunca_baja_de_40_minutos()
        => new EntraxOptions { CallbackToleranceMinutes = 5 }.CallbackTolerance.Should().Be(TimeSpan.FromMinutes(40));
}
