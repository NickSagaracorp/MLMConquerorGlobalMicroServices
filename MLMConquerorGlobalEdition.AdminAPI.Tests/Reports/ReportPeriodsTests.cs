using MLMConquerorGlobalEdition.Repository.Services.Reports;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

public class ReportPeriodsTests
{
    [Fact]
    public void Meses_completos_del_mes_inicial_al_final()
    {
        var meses = ReportPeriods.Months(new DateTime(2026, 9, 15), new DateTime(2026, 11, 14));

        meses.Select(m => m.Label).Should().Equal("Sep 2026", "Oct 2026", "Nov 2026");
        (meses[0].Start, meses[0].End).Should().Be((new DateTime(2026, 9, 1), new DateTime(2026, 9, 30)));
        meses[^1].End.Should().Be(new DateTime(2026, 11, 30));
    }

    [Fact]
    public void Las_semanas_empiezan_el_lunes_y_se_cortan_en_el_mes()
    {
        var semanas = ReportPeriods.WeeksOfMonth(2026, 11);

        semanas.Select(s => (s.Start.Day, s.End.Day)).Should().Equal((1, 1), (2, 8), (9, 15), (16, 22), (23, 29), (30, 30));
        semanas.Select(s => s.Label).Should().Equal("Nov 1", "Nov 2-8", "Nov 9-15", "Nov 16-22", "Nov 23-29", "Nov 30");
    }

    [Theory]
    [InlineData("2026-11-14", "2026-11-09", "2026-11-15")]
    [InlineData("2026-11-30", "2026-11-30", "2026-11-30")]   // lunes 30: la semana se corta en el fin de mes
    [InlineData("2026-12-02", "2026-12-01", "2026-12-06")]   // la misma semana del calendario, del lado de diciembre
    public void La_semana_de_una_fecha_queda_dentro_de_su_mes(string fecha, string inicio, string fin)
    {
        var semana = ReportPeriods.WeekOf(DateTime.Parse(fecha));

        (semana.Start, semana.End).Should().Be((DateTime.Parse(inicio), DateTime.Parse(fin)));
    }

    [Fact]
    public void Los_dias_son_los_de_la_semana_cortada()
    {
        var completa = ReportPeriods.DaysOfWeek(new DateTime(2026, 11, 12));
        var cortada  = ReportPeriods.DaysOfWeek(new DateTime(2026, 12, 2));

        completa.Should().HaveCount(7);
        completa[0].Label.Should().Be("Mon Nov 9");
        cortada.Select(d => d.Start.Day).Should().Equal(1, 2, 3, 4, 5, 6);
        cortada[0].Label.Should().Be("Tue Dec 1");
    }
}
