using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MLMConquerorGlobalEdition.BizCenter.Tests.Binario;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.RankEngine.Features.GetRankProgress;
using MLMConquerorGlobalEdition.RankEngine.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;
using Xunit.Abstractions;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Features;

/// <summary>
/// Plan Q, Bloque 1: los calificados del equipo de inscripcion salen de UNA consulta (el rango del Node unido a las
/// suscripciones activas), no de una lista de ids en memoria que se agotaba con 450.000 (Plan P, Bloque D).
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerProgresoDeRangoTests
{
    private readonly ITestOutputHelper _salida;
    public SqlServerProgresoDeRangoTests(ITestOutputHelper salida) => _salida = salida;

    private sealed class Reloj : IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>Guarda el valor de cada parametro de cada comando que EF manda.</summary>
    private sealed class Capturador : DbCommandInterceptor
    {
        public readonly List<string> Valores = new();
        public readonly List<string> Textos = new();

        private void Guardar(DbCommand c)
        {
            Textos.Add(c.CommandText);
            foreach (DbParameter p in c.Parameters) Valores.Add(Convert.ToString(p.Value) ?? string.Empty);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r, CancellationToken ct = default)
        {
            Guardar(c);
            return ValueTask.FromResult(r);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand c, CommandEventData e, InterceptionResult<object> r, CancellationToken ct = default)
        {
            Guardar(c);
            return ValueTask.FromResult(r);
        }
    }

    [SqlServerFact]
    public async Task Los_calificados_del_equipo_salen_de_una_consulta_sin_la_lista_de_ids()
    {
        var cap = new Capturador();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(SqlServerReportes.ConnectionString).AddInterceptors(cap).Options);
        await using var tx = await db.Database.BeginTransactionAsync();

        var (r, a, b, c) = (SqlServerReportes.NuevoId("RQ"), SqlServerReportes.NuevoId("RQ"),
                            SqlServerReportes.NuevoId("RQ"), SqlServerReportes.NuevoId("RQ"));
        // A y C cuelgan de B: el unico hijo directo de R es B. Los puntos por rama (EnrollmentTeamPointsService) mandan
        // la lista de hijos directos, acotada por los patrocinados directos y no por el equipo; asi A y C solo pueden
        // aparecer en un parametro si se manda la lista del equipo.
        ReportesDb.Miembro(db, r);
        ReportesDb.Miembro(db, b, r);
        ReportesDb.Miembro(db, a, b);
        ReportesDb.Miembro(db, c, b);
        ReportesDb.Nodo(db, r, null, ReportesDb.Ruta(r), 1);
        ReportesDb.Nodo(db, b, r, ReportesDb.Ruta(r, b), 2);
        ReportesDb.Nodo(db, a, b, ReportesDb.Ruta(r, b, a), 3);
        ReportesDb.Nodo(db, c, b, ReportesDb.Ruta(r, b, c), 3);
        ReportesDb.Suscripcion(db, r, 2, MembershipStatus.Active, ReportesDb.Base);   // el propio no cuenta
        ReportesDb.Suscripcion(db, a, 2, MembershipStatus.Active, ReportesDb.Base);
        ReportesDb.Suscripcion(db, b, 2, MembershipStatus.Cancelled, ReportesDb.Base);
        ReportesDb.Suscripcion(db, c, 2, MembershipStatus.Active, ReportesDb.Base);
        ReportesDb.Suscripcion(db, c, 3, MembershipStatus.Active, ReportesDb.Base);   // se cuentan suscripciones, como hoy
        await db.SaveChangesAsync();
        cap.Valores.Clear();

        var handler = new GetRankProgressHandler(db, new Reloj(),
            new EnrollmentTeamPointsService(db), new PersonalCustomerPointsService(db));
        var res = await handler.Handle(new GetRankProgressQuery(r), CancellationToken.None);

        res.IsSuccess.Should().BeTrue(res.Error);
        res.Value!.CurrentMetrics.EnrollmentQualifiedTeamMembers.Should().Be(3);
        // EF 10 manda la lista como un parametro por id (o como un JSON con EF.Parameter): cualquiera de las dos formas
        // deja el id de A o el de C en algun valor. La consulta nueva solo manda el Node y el id del propio R.
        cap.Valores.Should().NotContain(v => v.Contains(a) || v.Contains(c),
            "la pertenencia al equipo se resuelve en SQL por el rango del Node, no con la lista de ids");
        await tx.RollbackAsync();
    }

    [SqlServerFact]
    public async Task Los_calificados_del_binario_salen_de_los_contadores_del_nodo()
    {
        var cap = new Capturador();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(SqlServerReportes.ConnectionString).AddInterceptors(cap).Options);
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = SqlServerReportes.NuevoId("RB");
        ReportesDb.Miembro(db, r);
        var nodo = ReportesDb.NodoBinario(db, r, null, ReportesDb.Ruta(r), Domain.Enums.TreeSide.Left);
        (nodo.LeftLegQualified, nodo.RightLegQualified) = (4, 3);
        await db.SaveChangesAsync();
        cap.Textos.Clear();

        var res = await new GetRankProgressHandler(db, new Reloj(), new EnrollmentTeamPointsService(db),
            new PersonalCustomerPointsService(db)).Handle(new GetRankProgressQuery(r), CancellationToken.None);

        res.IsSuccess.Should().BeTrue(res.Error);
        res.Value!.CurrentMetrics.PlacementQualifiedTeamMembers.Should().Be(7);
        cap.Textos.Should().NotContain(t => t.Contains("DualTeamTree") && t.Contains("MembershipSubscriptions"),
            "los calificados del binario no se cuentan al vuelo (DQ13)");
        await tx.RollbackAsync();
    }

    /// <summary>
    /// Medicion (Categoria=Medicion: fuera de la suite normal, ver RankEngine.Tests.runsettings): mediana de 3
    /// ejecuciones del progreso completo de la raiz mayor local. Orden: la del resumen de Medicion.runsettings.
    /// </summary>
    [SqlServerFact]
    [Trait("Categoria", "Medicion")]
    public async Task El_progreso_de_la_raiz_mayor_local_responde_en_menos_de_un_segundo()
    {
        await using var db = SqlServerReportes.Db();
        var handler = new GetRankProgressHandler(db, new Reloj(),
            new EnrollmentTeamPointsService(db), new PersonalCustomerPointsService(db));
        await handler.Handle(new GetRankProgressQuery("AMB-700829"), CancellationToken.None);   // calienta
        var t = await EscalaDb.MedianaAsync(_salida, "Progreso de rango de AMB-700829 (local)", async _ =>
        {
            var r = await handler.Handle(new GetRankProgressQuery("AMB-700829"), CancellationToken.None);
            r.IsSuccess.Should().BeTrue(r.Error);
        });
        t.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }
}
