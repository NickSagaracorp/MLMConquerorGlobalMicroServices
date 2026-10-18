using System.Data;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using Xunit.Abstractions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>
/// Plan M: lo que se mide antes y después con la raíz real de la base local. Cada prueba imprime las
/// tres ejecuciones y su mediana, y falla si la mediana pasa del objetivo. ANTES del plan fallan: es
/// la línea base. En ReadUncommitted, como las de Reportes: se mide la consulta, no los bloqueos de
/// los servicios que haya levantados.
///
/// Son pruebas de tiempo: dependen de la máquina y de la carga, así que no corren con la suite.
/// <c>BizCenter.Tests.runsettings</c> las excluye por defecto (<c>Categoria!=Medicion</c>). Para medir:
/// <code>dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --settings MLMConquerorGlobalEdition.BizCenter.Tests/Medicion.runsettings --logger "console;verbosity=detailed"</code>
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
[Trait(Categoria, Medicion)]
public class SqlServerMedicionPlanMTests
{
    public const string Categoria = "Categoria";
    public const string Medicion  = "Medicion";

    public static readonly TimeSpan ObjetivoReporte = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan ObjetivoOrderNo = TimeSpan.FromMilliseconds(50);

    private readonly ITestOutputHelper _salida;

    public SqlServerMedicionPlanMTests(ITestOutputHelper salida) => _salida = salida;

    /// <summary>
    /// La raíz con el equipo más grande (en local, AMB-700829, con unas 119.400 personas debajo). La de
    /// nivel 0 (ROOT001) solo tiene unas decenas: medir con ella no dice nada del equipo grande, y así
    /// se midió la línea base de la Tarea 1 (ver «Desviaciones»). Empate: por MemberId, determinista.
    /// </summary>
    internal static async Task<string> RaizAsync(AppDbContext db)
    {
        var raices = await db.GenealogyTree.AsNoTracking()
            .Where(g => g.ParentMemberId == null && g.Node != null)
            .Select(g => new { g.MemberId, g.Node })
            .ToListAsync();
        var equipos = new List<(string MemberId, int Equipo)>();
        foreach (var r in raices)
            equipos.Add((r.MemberId, await db.GenealogyTree.AsNoTracking().Under(r.Node).CountAsync()));
        return equipos.OrderByDescending(e => e.Equipo).ThenBy(e => e.MemberId, StringComparer.Ordinal).First().MemberId;
    }

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

    /// <summary>Revisión I5: Mi equipo, ya por CurrentUnder (solo nodos vigentes), con el equipo grande.</summary>
    [SqlServerFact]
    public Task Mi_equipo_de_la_raiz() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);
        var raiz = await RaizAsync(db);
        var servicio = ClientesDelEquipoTests.Servicio(db);

        var t = await MedianaAsync($"Mi equipo {raiz}", () =>
            servicio.GetMyTeamGridAsync(raiz, new GridDataRequest { PageSize = 20 }));

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

    /// <summary>
    /// Revisión I-3: Mi equipo del Dual Team por los dos caminos. El subárbol más grande que cabe en la
    /// CTE (tope de 20.000) va por la CTE; la raíz del binario, por la ruta. Objetivo del plan: 2 s para
    /// el primero; la raíz se mide con el de los reportes (3 s) y queda anotada como pendiente de escala.
    /// </summary>
    [SqlServerFact]
    public Task Mi_equipo_dual_de_un_subarbol_que_cabe_en_la_CTE() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);
        var (miembro, nodos) = await SqlServerDualSubtreeTests.SubarbolMayorQueCabeEnLaCteAsync(db);
        var servicio = new DualTeamService(db, CalificadoEnLosEquiposTests.Rangos(db), CalificadoEnLosEquiposTests.Calificacion(db));

        var t = await MedianaAsync($"Mi equipo Dual {miembro} ({nodos} nodos, CTE)", () =>
            servicio.GetMyTeamGridAsync(miembro, new GridDataRequest { PageSize = 20 }));

        t.Should().BeLessThan(TimeSpan.FromSeconds(2));
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task Mi_equipo_dual_de_la_raiz_por_la_ruta() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);
        var raiz = await RaizAsync(db);
        var servicio = new DualTeamService(db, CalificadoEnLosEquiposTests.Rangos(db), CalificadoEnLosEquiposTests.Calificacion(db));

        var t = await MedianaAsync($"Mi equipo Dual {raiz} (raíz, ruta)", () =>
            servicio.GetMyTeamGridAsync(raiz, new GridDataRequest { PageSize = 20 }));

        t.Should().BeLessThan(ObjetivoReporte);
        await tx.RollbackAsync();
    });
}
