using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Revision del bloque 5 (minor d): Actividad, Clientes y Eventos dan lo mismo que con la forma de antes del bloque 5 (la de
/// master: el nodo vigente por TOP 1 con IsDescendantOf o con la antijoin, los niveles por los pedidos y el pivote en
/// memoria), sobre un equipo pequeno sembrado en SQL Server dentro de una transaccion que se deshace. La forma de antes se
/// reproduce aqui tal cual.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerEquivalenciaDeReportesTests
{
    private static readonly DateTime Desde = new(2026, 3, 1);
    private static readonly DateTime Hasta = new(2026, 3, 31);

    /// <summary>El equipo vigente como antes de la Tarea 21: el rango menos los nodos que no son el mas reciente.</summary>
    private static IQueryable<GenealogyEntity> VigentesComoAntes(AppDbContext db, HierarchyId root)
    {
        var arbol = db.GenealogyTree.AsNoTracking();
        return arbol.Under(root)
            .Where(g => !arbol.Any(o => o.MemberId == g.MemberId
                                     && (o.CreationDate > g.CreationDate
                                         || (o.CreationDate == g.CreationDate && string.Compare(o.Id, g.Id) > 0))));
    }

    /// <summary>
    /// El equipo de Actividad con nodos para los inscritos (clientes incluidos) y un nodo antiguo borrado de A bajo OTRO,
    /// como lo deja 01-nodos-de-inscripcion-repetidos.sql.
    /// </summary>
    private static async Task<(string Yo, string A, string Prefijo)> SembrarAsync(AppDbContext db)
    {
        var yo = SqlServerReportes.NuevoId();
        var a = SqlServerReportes.NuevoId();
        var otro = SqlServerReportes.NuevoId();
        var prefijo = SqlServerReportes.NuevoId("TQ")[..13];
        await EnrollmentActivityServiceTests.EquipoAsync(db, yo, a, otro, prefijo);
        ReportesDb.Nodo(db, $"{prefijo}1", yo, ReportesDb.Ruta(yo, $"{prefijo}1"), 2);
        ReportesDb.Nodo(db, $"{prefijo}2", yo, ReportesDb.Ruta(yo, $"{prefijo}2"), 2);
        ReportesDb.Nodo(db, $"{prefijo}4", a, ReportesDb.Ruta(yo, a, $"{prefijo}4"), 3);
        ReportesDb.Nodo(db, $"{prefijo}5", a, ReportesDb.Ruta(yo, a, $"{prefijo}5"), 3);
        ReportesDb.Nodo(db, $"{prefijo}6", otro, ReportesDb.Ruta(otro, $"{prefijo}6"), 2);
        await db.SaveChangesAsync();
        await ReportesDb.NodoObsoletoAsync(db, a, otro, ReportesDb.Ruta(otro, a), 2);
        return (yo, a, prefijo);
    }

    [SqlServerFact]
    public Task Actividad_da_lo_mismo_que_antes() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var (yo, _, _) = await SembrarAsync(db);

        var ahora = (await new EnrollmentActivityService(db, EnrollmentActivityServiceTests.Reloj)
                .GetGridAsync(yo, Desde, Hasta, new GridDataRequest { PageSize = 200 })).Value!.Items
            .Select(Fila).ToList();
        var antes = (await ActividadComoAntesAsync(db, yo)).Select(Fila).ToList();

        antes.Should().NotBeEmpty();
        ahora.Should().BeEquivalentTo(antes);
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task Clientes_del_equipo_dan_lo_mismo_que_antes() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var (yo, _, _) = await SembrarAsync(db);
        var root = await db.GenealogyTree.AsNoTracking().NodeOfAsync(yo);

        var ahora = (await ClientesDelEquipoTests.Servicio(db)
                .GetCustomersGridAsync(yo, new GridDataRequest { PageSize = 200 }, CustomerScope.Team)).Items
            .Select(c => (c.MemberId, c.Level)).ToList();
        var nivelRaiz = await db.GenealogyTree.AsNoTracking().Where(g => g.MemberId == yo).Select(g => g.Level).SingleAsync();
        var antes = await (from g in VigentesComoAntes(db, root!)
                           join m in db.MemberProfiles.AsNoTracking() on g.MemberId equals m.MemberId
                           where m.MemberType == MemberType.ExternalMember && m.MemberId != yo
                           select new { m.MemberId, Level = g.Level - nivelRaiz }).ToListAsync();

        antes.Should().HaveCount(2);
        ahora.Should().BeEquivalentTo(antes.Select(x => (x.MemberId, x.Level)));
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task Eventos_del_equipo_dan_lo_mismo_que_antes() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var id = new TeamEventRegistrationsServiceTests.Ids(
            SqlServerReportes.NuevoId(), SqlServerReportes.NuevoId(), SqlServerReportes.NuevoId(),
            SqlServerReportes.NuevoId(), SqlServerReportes.NuevoId(),
            "TQ-EV-" + Guid.NewGuid().ToString("N")[..12], "TQ-EV-" + Guid.NewGuid().ToString("N")[..12]);
        await TeamEventRegistrationsServiceTests.EscenarioAsync(db, id, sembrarRango: false);
        await ReportesDb.NodoObsoletoAsync(db, id.Fuera, id.Yo, ReportesDb.Ruta(id.Yo, id.Fuera), 2);
        var servicio = TeamEventRegistrationsServiceTests.Servicio(db);
        var root = await db.GenealogyTree.AsNoTracking().NodeOfAsync(id.Yo);

        var todo = (await servicio.GetGridAsync(id.Yo, id.Evento, false, new GridDataRequest { PageSize = 200 })).Value!;
        var inscritos = (await servicio.GetGridAsync(id.Yo, id.Evento, true, new GridDataRequest { PageSize = 200 })).Value!;
        var totales = (await servicio.GetTotalsAsync(id.Yo, id.Evento)).Value!;

        var regs = db.CorporateEventRegistrations.AsNoTracking().Where(r => r.EventId == id.Evento);
        var antes = await (from g in VigentesComoAntes(db, root!)
                           join m in db.MemberProfiles.AsNoTracking() on g.MemberId equals m.MemberId
                           select new
                           {
                               m.MemberId,
                               Registered = regs.Any(r => r.MemberId == m.MemberId),
                               Tickets = regs.Where(r => r.MemberId == m.MemberId).Sum(r => (int?)r.Tickets) ?? 0
                           }).ToListAsync();

        antes.Should().HaveCount(3);
        todo.Items.Select(f => (f.MemberId, f.Registered, f.Tickets))
            .Should().BeEquivalentTo(antes.Select(x => (x.MemberId, x.Registered, x.Tickets)));
        todo.TotalCount.Should().Be(antes.Count);
        inscritos.Items.Select(f => (f.MemberId, f.Tickets))
            .Should().BeEquivalentTo(antes.Where(x => x.Registered).Select(x => (x.MemberId, x.Tickets)));
        (totales.Registered, totales.Tickets).Should().Be((antes.Count(x => x.Registered), antes.Sum(x => x.Tickets)));
        await tx.RollbackAsync();
    });

    private static object Fila(EnrollmentActivityRowView f) => new
    {
        f.MemberId, f.FullName, f.Level, f.Customers, f.Ambassadors, f.Level1, f.Level2, f.Level3, f.Level4, f.OtherLevel,
        f.EnrollmentTeamPoints
    };

    /// <summary>EnrollmentActivityService.GetGridAsync de master (antes del bloque 5), sin la rejilla.</summary>
    private static async Task<List<EnrollmentActivityRowView>> ActividadComoAntesAsync(AppDbContext db, string memberId)
    {
        ReportDateRange.TryCreateForActivity(Desde, Hasta, EnrollmentActivityServiceTests.Reloj.Now, out var range, out _);
        var me = await db.GenealogyTree.AsNoTracking()
            .Where(g => g.MemberId == memberId)
            .OrderByDescending(g => g.CreationDate).ThenByDescending(g => g.Id)
            .Select(g => new { g.Node, g.Level }).FirstAsync();
        var start = range.Start;
        var end = range.End;
        var root = me.Node!;

        var rows = await (
                from m in db.MemberProfiles.AsNoTracking()
                where m.EnrollDate >= start && m.EnrollDate < end && m.SponsorMemberId != null
                let sponsorNode = db.GenealogyTree
                    .Where(g => g.MemberId == m.SponsorMemberId)
                    .OrderByDescending(g => g.CreationDate)
                    .ThenByDescending(g => g.Id)
                    .Select(g => new { g.Node, g.Level })
                    .FirstOrDefault()
                where sponsorNode != null && sponsorNode.Node!.IsDescendantOf(root)
                select new { m.MemberId, SponsorMemberId = m.SponsorMemberId!, m.MemberType, SponsorLevel = sponsorNode!.Level })
            .ToListAsync();

        var enrolledIds = rows.Select(r => r.MemberId).Distinct().ToList();
        var sponsorIds = rows.Select(r => r.SponsorMemberId).Distinct().ToList();
        var membershipOrders = await (
                from o in db.Orders.AsNoTracking()
                where enrolledIds.Contains(o.MemberId)
                join od in db.OrderDetails.AsNoTracking() on o.Id equals od.OrderId
                join p in db.Products.AsNoTracking() on od.ProductId equals p.Id
                where p.MembershipLevelId != null
                group p.MembershipLevelId!.Value by new { o.Id, o.MemberId, o.OrderDate, o.CreationDate } into g
                select new { g.Key.MemberId, g.Key.OrderDate, g.Key.CreationDate, LevelId = g.Max() })
            .ToListAsync();
        var signupLevel = membershipOrders.GroupBy(o => o.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderBy(o => o.OrderDate).ThenBy(o => o.CreationDate).First().LevelId);
        var names = await db.MemberProfiles.AsNoTracking()
            .Where(p => sponsorIds.Contains(p.MemberId))
            .Select(p => new { p.MemberId, FullName = p.FirstName + " " + p.LastName })
            .ToDictionaryAsync(x => x.MemberId, x => x.FullName);
        var points = (await db.MemberStatistics.AsNoTracking()
                .Where(st => sponsorIds.Contains(st.MemberId)).Select(st => new { st.MemberId, st.EnrollmentPoints }).ToListAsync())
            .GroupBy(x => x.MemberId).ToDictionary(g => g.Key, g => g.First().EnrollmentPoints);

        return rows.GroupBy(r => r.SponsorMemberId).Select(g =>
        {
            var row = new EnrollmentActivityRowView
            {
                MemberId = g.Key,
                FullName = names.GetValueOrDefault(g.Key, g.Key),
                Level = g.Min(x => x.SponsorLevel) - me.Level,
                EnrollmentTeamPoints = points.GetValueOrDefault(g.Key)
            };
            foreach (var e in g)
            {
                if (e.MemberType == MemberType.ExternalMember) row.Customers++;
                else row.Ambassadors++;
                switch (signupLevel.GetValueOrDefault(e.MemberId))
                {
                    case 1: row.Level1++; break;
                    case 2: row.Level2++; break;
                    case 3: row.Level3++; break;
                    case 4: row.Level4++; break;
                    default: row.OtherLevel++; break;
                }
            }
            return row;
        }).ToList();
    }
}
