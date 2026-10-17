using System.Data;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using Xunit.Abstractions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>
/// Plan M: lo que se mide antes y después con la raíz real de la base local. Cada prueba imprime las
/// tres ejecuciones y su mediana, y falla si la mediana pasa del objetivo. ANTES del plan fallan: es
/// la línea base. En ReadUncommitted, como las de Reportes: se mide la consulta, no los bloqueos de
/// los servicios que haya levantados.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerMedicionPlanMTests
{
    public static readonly TimeSpan ObjetivoReporte = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan ObjetivoOrderNo = TimeSpan.FromMilliseconds(50);

    private readonly ITestOutputHelper _salida;

    public SqlServerMedicionPlanMTests(ITestOutputHelper salida) => _salida = salida;

    internal static Task<string> RaizAsync(AppDbContext db) =>
        db.GenealogyTree.AsNoTracking()
            .Where(g => g.ParentMemberId == null).OrderBy(g => g.Level).Select(g => g.MemberId).FirstAsync();

    private async Task<TimeSpan> MedianaAsync(string que, Func<Task> paso)
    {
        var tiempos = new List<TimeSpan>();
        for (var i = 0; i < 3; i++)
        {
            var cronometro = Stopwatch.StartNew();
            await paso();
            cronometro.Stop();
            tiempos.Add(cronometro.Elapsed);
        }
        var mediana = tiempos.OrderBy(t => t).ElementAt(1);
        _salida.WriteLine($"{que}: {string.Join(" · ", tiempos.Select(t => $"{t.TotalMilliseconds:0} ms"))} → mediana {mediana.TotalMilliseconds:0} ms");
        return mediana;
    }

    [SqlServerFact]
    public Task Actividad_de_la_raiz_con_tres_meses() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);
        var raiz  = await RaizAsync(db);
        var reloj = new RelojFijo(DateTime.Now); // extremo de un rango de medición, no lógica de negocio
        var servicio = new EnrollmentActivityService(db, reloj);

        var t = await MedianaAsync($"Actividad {raiz}", async () =>
            (await servicio.GetGridAsync(raiz, ReportDateRange.ActivityMinFrom(reloj.Now), reloj.Now.Date,
                new GridDataRequest { PageSize = 20 })).IsSuccess.Should().BeTrue());

        t.Should().BeLessThan(ObjetivoReporte);
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task Clientes_de_todo_el_equipo_de_la_raiz() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);
        var raiz = await RaizAsync(db);
        var servicio = ClientesDelEquipoTests.Servicio(db);

        var t = await MedianaAsync($"Clientes {raiz}", () =>
            servicio.GetCustomersGridAsync(raiz, new GridDataRequest { PageSize = 20 }, CustomerScope.Team));

        t.Should().BeLessThan(ObjetivoReporte);
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task Eventos_con_todo_el_equipo_de_la_raiz() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);
        var raiz   = await RaizAsync(db);
        var evento = "TM-EV-" + Guid.NewGuid().ToString("N")[..12];
        db.CorporateEvents.Add(new CorporateEvent { Id = evento, Title = "Medición Plan M", EventDate = new DateTime(2026, 12, 1), IsActive = true, CreatedBy = "prueba" });
        await db.SaveChangesAsync();
        var servicio = TeamEventRegistrationsServiceTests.Servicio(db);

        var t = await MedianaAsync($"Eventos {raiz} (todo el equipo)", async () =>
        {
            (await servicio.GetGridAsync(raiz, evento, registeredOnly: false, new GridDataRequest { PageSize = 20 })).IsSuccess.Should().BeTrue();
            (await servicio.GetTotalsAsync(raiz, evento)).IsSuccess.Should().BeTrue();
        });

        t.Should().BeLessThan(ObjetivoReporte);
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task Comprobacion_de_numero_de_pedido() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);
        var numero = OrderNumberHelper.Generate("Medicion Plan M", DateTime.Now);

        var t = await MedianaAsync("OrderNo AnyAsync", async () =>
            (await db.Orders.AnyAsync(o => o.OrderNo == numero)).Should().BeFalse());

        t.Should().BeLessThan(ObjetivoOrderNo);
        await tx.RollbackAsync();
    });
}
