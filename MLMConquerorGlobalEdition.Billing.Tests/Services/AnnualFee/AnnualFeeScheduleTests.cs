using FluentAssertions;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>El calendario de la cuota (Plan K, K3, K4 revisado por Nick, K14): sin cobros hacia atrás; tarde se re-ancla al pago.</summary>
public class AnnualFeeScheduleTests
{
    private static DateTime D(string s) => DateTime.Parse(s);

    [Theory]
    [InlineData("2026-06-05 14:30", "2026-10-17 09:00", "2027-06-05")] // menos de un año: el primer aniversario
    [InlineData("2025-10-16 23:34", "2026-10-17 09:00", "2027-10-16")] // el aniversario fue ayer: no se cobra hacia atrás
    [InlineData("2025-10-17 08:00", "2026-10-17 09:00", "2026-10-17")] // el aniversario es hoy: vence hoy
    [InlineData("2018-03-02 10:00", "2026-10-17 09:00", "2027-03-02")] // muy antiguo: el próximo aniversario
    [InlineData("2020-02-29 10:00", "2026-10-17 09:00", "2027-02-28")] // 29 de febrero
    public void El_primer_vencimiento_al_crear_el_estado_es_el_primer_aniversario_que_no_ha_pasado(
        string alta, string ahora, string esperado)
        => AnnualFeeSchedule.FirstDueOnCreation(D(alta), D(ahora)).Should().Be(D(esperado));

    [Theory]
    // K4 revisado por Nick (2026-10-17): puntual o adelantado suma un año al vencimiento; tarde, un año desde el pago.
    [InlineData("2027-06-05", "2027-05-06 10:00", "2028-06-05")] // 30 días antes: no pierde días
    [InlineData("2027-06-05", "2027-05-20 10:00", "2028-06-05")] // antes del vencimiento
    [InlineData("2027-06-05", "2027-06-05 23:59", "2028-06-05")] // el mismo día
    [InlineData("2027-06-05", "2027-06-06 05:00", "2028-06-06")] // un día tarde (reintento): se re-ancla al pago
    [InlineData("2027-06-05", "2027-07-05 23:00", "2028-07-05")] // 30 días tarde: sin gracia, se re-ancla
    [InlineData("2027-06-05", "2028-01-15 12:00", "2029-01-15")] // estado parado y pago meses después
    [InlineData("2027-02-28", "2028-02-29 12:00", "2029-02-28")] // tarde un 29 de febrero
    public void El_siguiente_vencimiento_tras_pagar(string ciclo, string pagado, string esperado)
        => AnnualFeeSchedule.NextDueAfterPayment(D(ciclo), D(pagado)).Should().Be(D(esperado));

    [Theory]
    [InlineData("2027-07-10", "2027-06-10 09:00", true)]  // vence en 30 días
    [InlineData("2027-07-11", "2027-06-10 09:00", false)] // vence en 31 días
    [InlineData("2027-06-10", "2027-06-10 09:00", true)]  // vence hoy
    [InlineData("2026-01-01", "2027-06-10 09:00", true)]  // vencida hace mucho
    public void Se_puede_pagar_desde_30_dias_antes(string vence, string ahora, bool esperado)
        => AnnualFeeSchedule.IsPayable(D(vence), D(ahora)).Should().Be(esperado);

    [Fact]
    public void El_primer_aniversario_es_la_fecha_de_alta_mas_un_anio_sin_hora()
        => AnnualFeeSchedule.FirstAnniversary(D("2026-06-05 14:30")).Should().Be(D("2027-06-05"));
}
