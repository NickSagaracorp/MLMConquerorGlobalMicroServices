using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

public class ReportDateRangeTests
{
    private static readonly DateTime Ahora = new(2026, 10, 16, 15, 30, 0);

    [Fact]
    public void Un_solo_dia_va_de_su_medianoche_a_la_siguiente_e_ignora_la_hora()
    {
        ReportDateRange.TryCreate(new DateTime(2026, 3, 5, 18, 0, 0), new DateTime(2026, 3, 5, 9, 0, 0), out var r, out var error)
            .Should().BeTrue();
        error.Should().BeNull();
        r.Start.Should().Be(new DateTime(2026, 3, 5));
        r.End.Should().Be(new DateTime(2026, 3, 6));
    }

    [Fact]
    public void Hasta_antes_que_desde_no_vale()
    {
        ReportDateRange.TryCreate(new DateTime(2026, 3, 5), new DateTime(2026, 3, 4), out _, out var error).Should().BeFalse();
        error.Should().Be(ReportDateRange.InvalidCode);
    }

    [Fact]
    public void Sin_fechas_no_vale()
    {
        ReportDateRange.TryCreate(default, new DateTime(2026, 3, 4), out _, out var error).Should().BeFalse();
        error.Should().Be(ReportDateRange.InvalidCode);
    }

    [Fact]
    public void La_regla_comun_admite_366_dias_y_no_367()
    {
        ReportDateRange.TryCreate(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31), out _, out _).Should().BeTrue();
        ReportDateRange.TryCreate(new DateTime(2024, 1, 1), new DateTime(2025, 1, 1), out _, out var error).Should().BeFalse();
        error.Should().Be(ReportDateRange.TooLongCode);
    }

    // ── Actividad por fechas (corrección de Nick N4): 3 meses hacia atrás y nada en el futuro ──

    [Fact]
    public void Actividad_admite_justo_tres_meses_atras_y_no_un_dia_antes()
    {
        ReportDateRange.TryCreateForActivity(new DateTime(2026, 7, 16), new DateTime(2026, 10, 16), Ahora, out var r, out _)
            .Should().BeTrue();
        r.Start.Should().Be(new DateTime(2026, 7, 16));
        r.End.Should().Be(new DateTime(2026, 10, 17));

        ReportDateRange.TryCreateForActivity(new DateTime(2026, 7, 15), new DateTime(2026, 10, 16), Ahora, out _, out var error)
            .Should().BeFalse();
        error.Should().Be(ReportDateRange.TooOldCode);
    }

    [Fact]
    public void Actividad_no_admite_un_fin_posterior_a_hoy()
    {
        ReportDateRange.TryCreateForActivity(new DateTime(2026, 10, 1), new DateTime(2026, 10, 17), Ahora, out _, out var error)
            .Should().BeFalse();
        error.Should().Be(ReportDateRange.InFutureCode);
    }

    [Fact]
    public void Actividad_tambien_rechaza_un_rango_al_reves()
    {
        ReportDateRange.TryCreateForActivity(new DateTime(2026, 10, 5), new DateTime(2026, 10, 4), Ahora, out _, out var error)
            .Should().BeFalse();
        error.Should().Be(ReportDateRange.InvalidCode);
    }

    [Fact]
    public void Por_defecto_del_dia_uno_del_mes_a_hoy_segun_el_servidor()
    {
        var setup = ReportDateRange.SetupFor(Ahora);

        setup.DefaultFrom.Should().Be(new DateTime(2026, 10, 1));
        setup.DefaultTo.Should().Be(new DateTime(2026, 10, 16));
        setup.MaxRangeDays.Should().Be(366);
        setup.ActivityMinFrom.Should().Be(new DateTime(2026, 7, 16));
    }

    [Fact]
    public void El_rango_por_defecto_vale_para_actividad()
    {
        var setup = ReportDateRange.SetupFor(Ahora);

        ReportDateRange.TryCreateForActivity(setup.DefaultFrom, setup.DefaultTo, Ahora, out _, out _).Should().BeTrue();
    }
}
