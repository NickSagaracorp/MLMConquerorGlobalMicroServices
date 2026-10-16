using System.Data;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using Xunit.Abstractions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Lo que InMemory no prueba: que las altas del equipo, las órdenes de alta con productos de
/// membresía y su nivel más alto (N2) se traducen a SQL, y cuánto tarda la raíz real con el rango
/// máximo de Actividad (3 meses, N4).
/// </summary>
public class SqlServerActividadTests
{
    private readonly ITestOutputHelper _salida;

    public SqlServerActividadTests(ITestOutputHelper salida) => _salida = salida;

    [SqlServerFact]
    public async Task La_actividad_se_traduce_a_SQL_y_cuenta_lo_mismo_que_en_memoria()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var yo = SqlServerReportes.NuevoId();
        var a  = SqlServerReportes.NuevoId();
        await EnrollmentActivityServiceTests.EquipoAsync(db, yo, a, SqlServerReportes.NuevoId(), SqlServerReportes.NuevoId("TN")[..13]);

        var r = await new EnrollmentActivityService(db, EnrollmentActivityServiceTests.Reloj).GetGridAsync(
            yo, new DateTime(2026, 3, 1), new DateTime(2026, 3, 31), new GridDataRequest { PageSize = 50 });

        r.IsSuccess.Should().BeTrue();
        var filas = r.Value!.Items.ToDictionary(f => f.MemberId);
        (filas[yo].Ambassadors, filas[yo].Customers, filas[yo].Level1, filas[yo].Level2, filas[yo].Level3)
            .Should().Be((1, 1, 1, 1, 0));
        (filas[a].Level4, filas[a].OtherLevel, filas[a].EnrollmentTeamPoints).Should().Be((1, 1, 40));

        await tx.RollbackAsync();
    }

    /// <summary>
    /// R5: mide la raíz real con el rango máximo de Actividad. Si pasa de 3 s, abrir la tarea de
    /// agregar en SQL. Se mide en ReadUncommitted porque lo que interesa es el coste de la consulta:
    /// con servicios levantados contra la base local (el bucle de unicidad de OrderNo de R9), una
    /// lectura en ReadCommitted que recorre Orders y OrderDetails sin índice acaba víctima de un
    /// deadlock y la medición no dice nada.
    /// </summary>
    [SqlServerFact]
    public async Task La_raiz_con_tres_meses_cabe_en_tiempo()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);
        var raiz = await db.GenealogyTree.AsNoTracking()
            .Where(g => g.ParentMemberId == null).OrderBy(g => g.Level).Select(g => g.MemberId).FirstAsync();
        // DateTime.Now aquí es solo el extremo de un rango de medición, no lógica de negocio.
        var reloj = new RelojFijo(DateTime.Now);
        var hoy   = reloj.Now.Date;

        var cronometro = Stopwatch.StartNew();
        var r = await new EnrollmentActivityService(db, reloj).GetGridAsync(
            raiz, ReportDateRange.ActivityMinFrom(reloj.Now), hoy, new GridDataRequest { PageSize = 20 });
        cronometro.Stop();

        r.IsSuccess.Should().BeTrue();
        _salida.WriteLine($"Actividad de {raiz} en 3 meses: {r.Value!.TotalCount} filas en {cronometro.ElapsedMilliseconds} ms");
        cronometro.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));

        await tx.RollbackAsync();
    }
}
