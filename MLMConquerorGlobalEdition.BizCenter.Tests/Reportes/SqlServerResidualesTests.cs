using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Commissions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Lo que InMemory no prueba: que el GroupBy por estado, el enum.ToString() de la proyección y el
/// GroupBy por año y mes del gráfico se traducen a SQL, y que el índice existe en la base.
/// </summary>
public class SqlServerResidualesTests
{
    [SqlServerFact]
    public async Task El_historial_y_el_grafico_se_traducen_a_SQL()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var yo = SqlServerReportes.NuevoId();
        db.DailyResidualEarnings.AddRange(
            DailyResidualReportServiceTests.Residual(yo, new DateTime(2025, 5, 3), 10m, CommissionEarningStatus.Paid,
                dt: 100, et: 0, pagado: new DateTime(2025, 5, 10), comentario: "Consolidated"),
            DailyResidualReportServiceTests.Residual(yo, new DateTime(2025, 5, 4), 5m),
            DailyResidualReportServiceTests.Residual(yo, new DateTime(2025, 6, 1), 7m, CommissionEarningStatus.Cancelled));
        await db.SaveChangesAsync();
        var reloj = new RelojFijo(new DateTime(2025, 6, 15, 12, 0, 0));

        var historial = await new DailyResidualReportService(db, reloj).GetHistoryAsync(yo, 2025, 5, 1, 10);
        var grafico   = await new CommissionsService(db, reloj).GetDualResidualChartAsync(yo, 2);

        historial.TotalCount.Should().Be(2);
        historial.PeriodPaid.Should().Be(10m);
        historial.PeriodPending.Should().Be(5m);
        historial.Items[0].Status.Should().Be("Pending");
        historial.Items[0].EligiblePoints.Should().BeNull();
        historial.Items[1].EligiblePoints.Should().Be(100);
        grafico.Select(p => p.Amount).Should().Equal(15m, 0m);

        await tx.RollbackAsync();
    }

    [SqlServerFact]
    public async Task El_indice_por_beneficiario_y_fecha_existe()
    {
        await using var db = SqlServerReportes.Db();

        var n = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM sys.indexes
            WHERE name = 'IX_DailyResidualEarnings_BeneficiaryMemberId_EarnedDate'
              AND object_id = OBJECT_ID('DailyResidualEarnings')
            """).SingleAsync();

        n.Should().Be(1, "la migración IndiceResidualesPorBeneficiarioYFecha tiene que estar aplicada en local (Tarea 8)");

        var incluidas = await db.Database.SqlQuery<string>($"""
            SELECT c.name AS [Value] FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.name = 'IX_DailyResidualEarnings_BeneficiaryMemberId_EarnedDate'
              AND i.object_id = OBJECT_ID('DailyResidualEarnings') AND ic.is_included_column = 1
            """).ToListAsync();

        incluidas.Should().BeEquivalentTo(new[] { "Amount", "Status" },
            "la migración IndiceResidualesConImporteYEstado tiene que estar aplicada en local");
    }
}
