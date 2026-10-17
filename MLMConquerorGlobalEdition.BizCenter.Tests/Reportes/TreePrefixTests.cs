using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// El prefijo de un subárbol termina SIEMPRE en "/": sin ella, "/AMB-1" casaría también con
/// "/AMB-10/..." y metería en el equipo a quien no lo es.
/// </summary>
public class TreePrefixTests
{
    [Theory]
    [InlineData("/AMB-1",           "/AMB-1/")]
    [InlineData("/AMB-1/",          "/AMB-1/")]
    [InlineData("/ROOT/AMB-1",      "/ROOT/AMB-1/")]
    public void Of_termina_siempre_en_barra(string ruta, string esperado)
        => TreePrefix.Of(ruta).Should().Be(esperado);

    [Fact]
    public void Con_barra_el_prefijo_de_AMB_1_no_casa_con_AMB_10()
        => "/AMB-10/MBR-1/".StartsWith(TreePrefix.Of("/AMB-1"), StringComparison.Ordinal).Should().BeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Una_ruta_vacia_no_es_un_prefijo_valido(string? ruta)
        => FluentActions.Invoking(() => TreePrefix.Of(ruta!)).Should().Throw<ArgumentException>();
}
