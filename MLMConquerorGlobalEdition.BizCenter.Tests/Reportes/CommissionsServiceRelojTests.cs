using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Commissions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// CommissionsService tomaba el "ahora" de DateTime.UtcNow: el año en curso, la semana del bono
/// boost y el mes del bono del coche cambiaban de día a deshora. Ahora sale del reloj del servidor.
/// </summary>
public class CommissionsServiceRelojTests
{
    private const string Yo = "AMB-000001";
    private static readonly RelojFijo Reloj = new(new DateTime(2025, 6, 18, 12, 0, 0)); // miércoles

    [Fact]
    public async Task El_grafico_suma_residuales_diarios_por_mes_con_la_hora_de_servidor()
    {
        await using var db = ReportesDb.Nueva();
        db.DailyResidualEarnings.AddRange(
            DailyResidualReportServiceTests.Residual(Yo, new DateTime(2025, 5, 3), 10m),
            DailyResidualReportServiceTests.Residual(Yo, new DateTime(2025, 5, 20), 5m, CommissionEarningStatus.Paid),
            DailyResidualReportServiceTests.Residual(Yo, new DateTime(2025, 6, 7), 7m, CommissionEarningStatus.Cancelled),
            DailyResidualReportServiceTests.Residual(Yo, new DateTime(2026, 5, 1), 80m));
        db.CommissionEarnings.Add(new CommissionEarning
        {
            BeneficiaryMemberId = Yo, CommissionTypeId = 1, Amount = 999m,
            Status = CommissionEarningStatus.Paid, EarnedDate = new DateTime(2025, 5, 5), CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();

        var serie = await new CommissionsService(db, Reloj).GetDualResidualChartAsync(Yo, 3);

        serie.Select(p => (p.Year, p.Month)).Should().Equal((2025, 4), (2025, 5), (2025, 6));
        serie.Select(p => p.Amount).Should().Equal(0m, 15m, 0m);
    }

    [Fact]
    public async Task El_resumen_toma_el_anio_del_reloj()
    {
        await using var db = ReportesDb.Nueva();
        db.CommissionEarnings.AddRange(
            new CommissionEarning { BeneficiaryMemberId = Yo, CommissionTypeId = 1, Amount = 100m, Status = CommissionEarningStatus.Paid, EarnedDate = new DateTime(2025, 2, 1), CreatedBy = "prueba" },
            new CommissionEarning { BeneficiaryMemberId = Yo, CommissionTypeId = 1, Amount = 50m,  Status = CommissionEarningStatus.Paid, EarnedDate = new DateTime(2026, 2, 1), CreatedBy = "prueba" });
        await db.SaveChangesAsync();

        var resumen = await new CommissionsService(db, Reloj).GetSummaryAsync(Yo);

        resumen.CurrentYearTotal.Should().Be(100m);
    }

    [Fact]
    public async Task La_semana_del_bono_boost_sale_del_reloj()
    {
        await using var db = ReportesDb.Nueva();

        var semana = await new CommissionsService(db, Reloj).GetBoostBonusWeekStatsAsync(Yo);

        semana.WeekLabel.Should().Be($"Week of {new DateTime(2025, 6, 16):MMM d} - {new DateTime(2025, 6, 22):MMM d}");
    }

    [Fact]
    public async Task El_mes_del_bono_del_coche_sale_del_reloj()
    {
        await using var db = ReportesDb.Nueva();

        var coche = await new CommissionsService(db, Reloj).GetCarBonusStatsAsync(Yo);

        coche.MonthLabel.Should().Be(new DateTime(2025, 6, 18).ToString("MMMM yyyy"));
    }
}
