using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Commissions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// El historial de residuales lee DailyResidualEarnings (lo que escribe el motor), no
/// CommissionEarnings de la categoría 3, que mezcla débitos y consolidaciones.
/// </summary>
public class DailyResidualReportServiceTests
{
    private const string Yo = "AMB-000001";
    private static readonly RelojFijo Reloj = new(new DateTime(2025, 6, 15, 12, 0, 0));

    internal static DailyResidualEarning Residual(
        string beneficiario, DateTime fecha, decimal importe,
        CommissionEarningStatus estado = CommissionEarningStatus.Pending,
        int? dt = null, int? et = null, DateTime? pagado = null, string? comentario = null) => new()
    {
        BeneficiaryMemberId          = beneficiario,
        EarnedDate                   = fecha,
        Amount                       = importe,
        Status                       = estado,
        EligibleDualTeamPoints       = dt,
        EligibleEnrollmentTeamPoints = et,
        PaymentDate                  = pagado,
        PaymentComment               = comentario,
        CreatedBy                    = "prueba"
    };

    private static DailyResidualReportService Servicio(AppDbContext db) => new(db, Reloj);

    [Fact]
    public async Task Solo_lee_los_residuales_diarios_del_miembro()
    {
        await using var db = ReportesDb.Nueva();
        db.DailyResidualEarnings.AddRange(
            Residual(Yo, new DateTime(2025, 5, 3), 10m),
            Residual(Yo, new DateTime(2025, 5, 4), 5m),
            Residual("AMB-000002", new DateTime(2025, 5, 4), 50m));
        db.CommissionEarnings.Add(new CommissionEarning
        {
            BeneficiaryMemberId = Yo, CommissionTypeId = 1, Amount = 999m,
            Status = CommissionEarningStatus.Paid, EarnedDate = new DateTime(2025, 5, 5), CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();

        var vista = await Servicio(db).GetHistoryAsync(Yo, 2025, 5, 1, 10);

        vista.TotalCount.Should().Be(2);
        vista.PeriodTotal.Should().Be(15m);
    }

    [Fact]
    public async Task Filtra_por_mes_y_totaliza_pagado_y_pendiente()
    {
        await using var db = ReportesDb.Nueva();
        db.DailyResidualEarnings.AddRange(
            Residual(Yo, new DateTime(2025, 5, 3), 10m, CommissionEarningStatus.Paid, pagado: new DateTime(2025, 5, 12)),
            Residual(Yo, new DateTime(2025, 5, 4), 5m),
            Residual(Yo, new DateTime(2025, 4, 30), 70m));
        await db.SaveChangesAsync();

        var vista = await Servicio(db).GetHistoryAsync(Yo, 2025, 5, 1, 10);

        vista.Year.Should().Be(2025);
        vista.Month.Should().Be(5);
        vista.Items.Should().HaveCount(2);
        vista.PeriodPaid.Should().Be(10m);
        vista.PeriodPending.Should().Be(5m);
        vista.PeriodTotal.Should().Be(15m);
    }

    [Fact]
    public async Task Sin_anio_usa_el_del_reloj_de_servidor_y_sin_mes_todo_el_anio()
    {
        await using var db = ReportesDb.Nueva();
        db.DailyResidualEarnings.AddRange(
            Residual(Yo, new DateTime(2025, 1, 2), 1m),
            Residual(Yo, new DateTime(2025, 12, 31), 2m),
            Residual(Yo, new DateTime(2026, 3, 1), 4m));
        await db.SaveChangesAsync();

        var vista = await Servicio(db).GetHistoryAsync(Yo, null, null, 1, 10);

        vista.Year.Should().Be(2025);
        vista.Month.Should().BeNull();
        vista.PeriodTotal.Should().Be(3m);
    }

    [Fact]
    public async Task Un_anio_fuera_de_los_disponibles_cae_en_el_actual()
    {
        await using var db = ReportesDb.Nueva();

        var vista = await Servicio(db).GetHistoryAsync(Yo, 1990, 13, 1, 10);

        vista.Year.Should().Be(2025);
        vista.Month.Should().BeNull();
    }

    [Fact]
    public async Task Excluye_los_cancelados_de_filas_y_totales()
    {
        await using var db = ReportesDb.Nueva();
        db.DailyResidualEarnings.AddRange(
            Residual(Yo, new DateTime(2025, 5, 3), 10m),
            Residual(Yo, new DateTime(2025, 5, 4), 99m, CommissionEarningStatus.Cancelled));
        await db.SaveChangesAsync();

        var vista = await Servicio(db).GetHistoryAsync(Yo, 2025, 5, 1, 10);

        vista.Items.Should().ContainSingle();
        vista.PeriodTotal.Should().Be(10m);
    }

    [Fact]
    public async Task Los_puntos_elegibles_suman_los_dos_ejes_y_son_null_sin_dato()
    {
        await using var db = ReportesDb.Nueva();
        db.DailyResidualEarnings.AddRange(
            Residual(Yo, new DateTime(2025, 5, 3), 10m, dt: 0, et: 120),
            Residual(Yo, new DateTime(2025, 5, 4), 10m));
        await db.SaveChangesAsync();

        var vista = await Servicio(db).GetHistoryAsync(Yo, 2025, 5, 1, 10);

        vista.Items.Single(i => i.EarnedDate.Day == 3).EligiblePoints.Should().Be(120);
        vista.Items.Single(i => i.EarnedDate.Day == 4).EligiblePoints.Should().BeNull();
    }

    [Fact]
    public async Task Los_anios_disponibles_van_desde_el_alta_hasta_hoy()
    {
        await using var db = ReportesDb.Nueva();
        ReportesDb.Miembro(db, Yo, alta: new DateTime(2023, 2, 1));
        await db.SaveChangesAsync();

        (await Servicio(db).GetHistoryAsync(Yo, null, null, 1, 10))
            .AvailableYears.Should().Equal(2025, 2024, 2023);
        (await Servicio(db).GetHistoryAsync("AMB-999999", null, null, 1, 10))
            .AvailableYears.Should().Equal(2025);
    }

    [Fact]
    public async Task Pagina_por_fecha_descendente_con_tope_de_cien()
    {
        await using var db = ReportesDb.Nueva();
        for (var d = 1; d <= 25; d++)
            db.DailyResidualEarnings.Add(Residual(Yo, new DateTime(2025, 5, d), 1m));
        await db.SaveChangesAsync();

        var segunda = await Servicio(db).GetHistoryAsync(Yo, 2025, 5, 2, 10);
        segunda.Items.Should().HaveCount(10);
        segunda.Items.First().EarnedDate.Day.Should().Be(15);
        segunda.Items.Select(i => i.EarnedDate).Should().BeInDescendingOrder();

        (await Servicio(db).GetHistoryAsync(Yo, 2025, 5, 1, 500)).PageSize.Should().Be(100);
    }
}
