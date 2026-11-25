using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Formato;

public class NumeroCortoTests
{
    [Theory]
    [InlineData(1_000_003, "1,000,003")]
    [InlineData(148_560, "148,560")]
    [InlineData(0, "0")]
    public void La_cantidad_lleva_separador_de_miles(long valor, string esperado) =>
        NumeroCorto.Cantidad(valor).Should().Be(esperado);

    [Theory]
    [InlineData(61_354_648.34, "$61,354,648.34")]
    [InlineData(413, "$413.00")]
    [InlineData(-1200, "-$1,200.00")]
    public void El_monto_lleva_simbolo_miles_y_centavos(double valor, string esperado) =>
        NumeroCorto.Monto((decimal)valor).Should().Be(esperado);

    [Theory]
    [InlineData(148_560, "148,560")]    // cabe: completa
    [InlineData(1_000_003, "1M")]       // no cabe: abreviada sin decimales de más
    [InlineData(1_548_000, "1.54M")]    // truncada, no redondeada
    [InlineData(12_345_678, "12.34M")]
    [InlineData(999_999_999, "999.99M")]
    [InlineData(2_500_000_000, "2.5B")]
    public void La_cantidad_se_abrevia_solo_si_no_cabe(long valor, string esperado) =>
        NumeroCorto.Cantidad(valor, 7).Should().Be(esperado);

    [Theory]
    [InlineData(61_354_648.34, "$61.35M")]
    [InlineData(105_249_000, "$105.2M")]
    [InlineData(1_234.5, "$1.23K")]
    [InlineData(413.5, "$413.50")]     // cabe: completo con centavos
    [InlineData(54_321.99, "$54.32K")]
    [InlineData(-61_354_648.34, "-$61.3M")]
    [InlineData(0, "$0.00")]
    public void El_monto_se_abrevia_solo_si_no_cabe(double valor, string esperado) =>
        NumeroCorto.Monto((decimal)valor, 7).Should().Be(esperado);
}
