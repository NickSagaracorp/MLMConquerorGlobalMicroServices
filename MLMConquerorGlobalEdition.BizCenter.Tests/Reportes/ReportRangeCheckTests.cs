using MLMConquerorGlobalEdition.SharedComponents.Components.Team;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>La pantalla avisa antes de pedir un rango que el servidor rechazaría.</summary>
public class ReportRangeCheckTests
{
    private static readonly DateTime Hoy    = new(2026, 10, 16);
    private static readonly DateTime Minimo = new(2026, 7, 16);

    [Fact]
    public void Un_rango_valido_no_tiene_problema()
        => ReportRangeCheck.Problem(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31), 366).Should().BeNull();

    [Fact]
    public void Hasta_antes_que_desde()
        => ReportRangeCheck.Problem(new DateTime(2026, 3, 2), new DateTime(2026, 3, 1), 366)
            .Should().Be(("Reports.RangeInvalid", (object?)null));

    [Fact]
    public void Mas_dias_de_los_permitidos()
        => ReportRangeCheck.Problem(new DateTime(2024, 1, 1), new DateTime(2025, 1, 1), 366)
            .Should().Be(("Reports.RangeTooLong", (object?)366));

    // ── Actividad por fechas (N4) ──

    [Fact]
    public void Actividad_admite_desde_el_minimo_hasta_hoy()
        => ReportRangeCheck.ActivityProblem(Minimo, Hoy, Minimo, Hoy).Should().BeNull();

    [Fact]
    public void Actividad_no_admite_un_dia_antes_del_minimo()
        => ReportRangeCheck.ActivityProblem(Minimo.AddDays(-1), Hoy, Minimo, Hoy)
            .Should().Be(("Reports.RangeTooOld", (object?)Minimo.ToString("d")));

    [Fact]
    public void Actividad_no_admite_un_fin_futuro()
        => ReportRangeCheck.ActivityProblem(Minimo, Hoy.AddDays(1), Minimo, Hoy)
            .Should().Be(("Reports.RangeInFuture", (object?)null));

    [Fact]
    public void Actividad_comprueba_antes_que_el_rango_no_este_al_reves()
        => ReportRangeCheck.ActivityProblem(Hoy, Minimo, Minimo, Hoy)
            .Should().Be(("Reports.RangeInvalid", (object?)null));
}
