using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Jobs;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using OrdersEntity = MLMConquerorGlobalEdition.Domain.Entities.Orders.Orders;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Plan Q (DQ16, reglas de Nick): los puntos del producto cuentan en toda la linea (piernas del binario y EnrollmentPoints de
/// la linea de patrocinio, el propio incluido) solo mientras la membresia esta activa. Entrar en Active suma, salir resta,
/// renovar no cambia nada salvo que cambie el nivel (la diferencia). Se comprueban los dos arboles tras vaciar las dos
/// colas, dentro de una transaccion que se deshace.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerPuntosPorMembresiaTests
{
    private sealed class Reloj : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private sealed record Escenario(string Raiz, string Patrocinador, string Miembro);

    /// <summary>
    /// Revision 2 del bloque 2 (menor): con los servicios -run escribiendo en la misma base, una prueba larga podia caer por
    /// un fallo de transporte (la conexion cerrada en el RollbackAsync final) o ser victima de un interbloqueo. Se repite
    /// entera, con escenario nuevo (cada intento usa otro prefijo), y la transaccion se deshace al soltarla (sin
    /// RollbackAsync explicito: si la conexion se cayo, SQL Server ya la deshizo).
    /// </summary>
    private static async Task ConReintentoAsync(Func<Task> prueba, int intentos = 3)
    {
        for (var intento = 1; ; intento++)
        {
            try { await prueba(); return; }
            catch (Exception ex) when (intento < intentos && Transitorio(ex))
            {
                SqlConnection.ClearAllPools();
                await Task.Delay(1000 * intento);
            }
        }
    }

    private static bool Transitorio(Exception ex) => ex switch
    {
        SqlException s => s.Number is 1205 or -2 or -1 or 0 or 233 or 10053 or 10054 or 64 or 121,
        InvalidOperationException { InnerException: { } i } => Transitorio(i),
        DbUpdateException { InnerException: { } i } => Transitorio(i),
        _ => false
    };

    /// <summary>
    /// Inscripcion R → P → M (rutas de texto) y binario R ← P ← M por la izquierda; estadisticas a 0. M no se coloca
    /// si <paramref name="colocar"/> es false.
    /// </summary>
    private static async Task<Escenario> SembrarAsync(AppDbContext db, bool colocar = true)
    {
        var x = BinarioDb.Prefijo("PQP");
        var e = new Escenario($"{x}R", $"{x}P", $"{x}M");
        var ahora = DateTime.Now;
        db.GenealogyTree.AddRange(
            new GenealogyEntity { MemberId = e.Raiz, HierarchyPath = $"/{e.Raiz}/", Level = 0, CreatedBy = "plan-q-prueba", CreationDate = ahora },
            new GenealogyEntity { MemberId = e.Patrocinador, ParentMemberId = e.Raiz, HierarchyPath = $"/{e.Raiz}/{e.Patrocinador}/", Level = 1, CreatedBy = "plan-q-prueba", CreationDate = ahora },
            new GenealogyEntity { MemberId = e.Miembro, ParentMemberId = e.Patrocinador, HierarchyPath = $"/{e.Raiz}/{e.Patrocinador}/{e.Miembro}/", Level = 2, CreatedBy = "plan-q-prueba", CreationDate = ahora });
        foreach (var id in new[] { e.Raiz, e.Patrocinador, e.Miembro })
            db.MemberStatistics.Add(new MemberStatisticEntity { MemberId = id, CreatedBy = "plan-q-prueba", CreationDate = ahora });
        await db.SaveChangesAsync();
        await BinarioDb.SembrarPerfilAsync(db, e.Miembro, e.Patrocinador);
        var r = await DualTeamNodes.RootAsync(db, e.Raiz, ahora, "plan-q-prueba");
        db.DualTeamTree.Add(r);
        var p = await DualTeamNodes.ChildAsync(db, r, e.Patrocinador, TreeSide.Left, ahora, "plan-q-prueba");
        db.DualTeamTree.Add(p);
        if (colocar) db.DualTeamTree.Add(await DualTeamNodes.ChildAsync(db, p, e.Miembro, TreeSide.Left, ahora, "plan-q-prueba"));
        await db.SaveChangesAsync();
        return e;
    }

    private static async Task VaciarAsync(AppDbContext db)
    {
        await LegPointMaintenance.DrainAsync(db);
        await new ApplyMemberStatisticDeltasJob(db, new Reloj(), NullLogger<ApplyMemberStatisticDeltasJob>.Instance).ExecuteAsync();
        db.ChangeTracker.Clear();
    }

    /// <summary>Los puntos que ve la linea: (piernas izquierdas de R y de P, EnrollmentPoints de R, P y M, PersonalPoints de M).</summary>
    private static async Task<(decimal R, decimal P, int ER, int EP, int EM, int PM)> LineaAsync(AppDbContext db, Escenario e)
    {
        var r = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == e.Raiz);
        var p = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == e.Patrocinador);
        int E(string id) => db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == id).OrderBy(s => s.Id).Select(s => s.EnrollmentPoints).First();
        var pm = await db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == e.Miembro).OrderBy(s => s.Id).Select(s => s.PersonalPoints).FirstAsync();
        // DualTeamPoints es la copia de las piernas (no lo escribe nadie mas).
        var dtp = await db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == e.Raiz).OrderBy(s => s.Id).Select(s => s.DualTeamPoints).FirstAsync();
        dtp.Should().Be((int)(r.LeftLegPoints + r.RightLegPoints));
        return (r.LeftLegPoints, p.LeftLegPoints, E(e.Raiz), E(e.Patrocinador), E(e.Miembro), pm);
    }

    private static async Task CambiarAsync(AppDbContext db, string subId, Action<MembershipSubscription> cambio)
    {
        cambio(await db.MembershipSubscriptions.SingleAsync(s => s.Id == subId));
        await db.SaveChangesAsync();
        await VaciarAsync(db);
    }

    [SqlServerFact]
    public Task Alta_cancelacion_retencion_reactivaciones_y_renovacion() =>
        ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        db.Database.SetCommandTimeout(300);
        await using var tx = await db.Database.BeginTransactionAsync();
        var e = await SembrarAsync(db);
        var seis = await MembershipPointsDeltas.LevelPointsAsync(db, 3);
        seis.Should().BeGreaterThan(0, "el producto del nivel 3 tiene puntos de calificacion");

        var sus = ReportesDb.Suscripcion(db, e.Miembro, 3, MembershipStatus.Pending, DateTime.Now);
        await db.SaveChangesAsync();
        await VaciarAsync(db);
        (await LineaAsync(db, e)).Should().Be((0m, 0m, 0, 0, 0, 0), "pendiente de pago no cuenta");

        await CambiarAsync(db, sus.Id, s => s.SubscriptionStatus = MembershipStatus.Active);
        (await LineaAsync(db, e)).Should().Be((seis, seis, seis, seis, seis, seis), "el alta suma a toda la linea");

        await CambiarAsync(db, sus.Id, s => { s.StartDate = DateTime.Now.AddMonths(1); s.ChangeReason = SubscriptionChangeReason.Renewal; });
        (await LineaAsync(db, e)).Should().Be((seis, seis, seis, seis, seis, seis), "renovar sin cambiar de nivel no cambia nada");

        await CambiarAsync(db, sus.Id, s => s.SubscriptionStatus = MembershipStatus.HoldByBilling);
        (await LineaAsync(db, e)).Should().Be((0m, 0m, 0, 0, 0, 0), "retenida por cobro resta");

        await CambiarAsync(db, sus.Id, s => s.SubscriptionStatus = MembershipStatus.Active);
        (await LineaAsync(db, e)).Should().Be((seis, seis, seis, seis, seis, seis), "reactivar desde retenida suma");

        await CambiarAsync(db, sus.Id, s => s.SubscriptionStatus = MembershipStatus.Cancelled);
        (await LineaAsync(db, e)).Should().Be((0m, 0m, 0, 0, 0, 0), "cancelar resta");

        await CambiarAsync(db, sus.Id, s => s.SubscriptionStatus = MembershipStatus.Active);
        (await LineaAsync(db, e)).Should().Be((seis, seis, seis, seis, seis, seis), "reactivar desde cancelada suma (confirmado por Nick)");

    });

    [SqlServerFact]
    public Task Renovar_con_cambio_de_nivel_aplica_la_diferencia() =>
        ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        db.Database.SetCommandTimeout(300);
        await using var tx = await db.Database.BeginTransactionAsync();
        var e = await SembrarAsync(db);
        var p3 = await MembershipPointsDeltas.LevelPointsAsync(db, 3);
        var p2 = await MembershipPointsDeltas.LevelPointsAsync(db, 2);
        p2.Should().NotBe(p3);

        var sus = ReportesDb.Suscripcion(db, e.Miembro, 3, MembershipStatus.Active, DateTime.Now);
        await db.SaveChangesAsync();
        await VaciarAsync(db);
        (await LineaAsync(db, e)).Should().Be((p3, p3, p3, p3, p3, p3));

        // Renovacion que cambia el nivel en la misma fila.
        await CambiarAsync(db, sus.Id, s => { s.MembershipLevelId = 2; s.ChangeReason = SubscriptionChangeReason.Renewal; });
        (await LineaAsync(db, e)).Should().Be((p2, p2, p2, p2, p2, p2), "la diferencia p2 - p3");

        // Subida como la hace UpgradeMembershipHandler: la vieja caduca y nace otra activa, en el mismo guardado.
        var vieja = await db.MembershipSubscriptions.SingleAsync(s => s.Id == sus.Id);
        vieja.SubscriptionStatus = MembershipStatus.Expired;
        ReportesDb.Suscripcion(db, e.Miembro, 3, MembershipStatus.Active, DateTime.Now);
        await db.SaveChangesAsync();
        await VaciarAsync(db);
        (await LineaAsync(db, e)).Should().Be((p3, p3, p3, p3, p3, p3), "la diferencia p3 - p2");

    });

    /// <summary>
    /// Un miembro de antes de DQ16 (revision 2 del bloque 2, C1): <paramref name="puntos"/> en PersonalPoints, ya contados en
    /// las piernas de R y P y en los EnrollmentPoints de R, P y M (como los dejaban el alta y 02-recalcular-piernas.sql), y
    /// sus suscripciones sin aporte guardado. Se escribe por SQL despues de guardar las suscripciones, como estaba la base.
    /// </summary>
    private static async Task LegadoAsync(AppDbContext db, Escenario e, int puntos)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE MemberStatistics SET PersonalPoints = {puntos} WHERE MemberId = {e.Miembro};
UPDATE MemberStatistics SET EnrollmentPoints = {puntos} WHERE MemberId IN ({e.Raiz}, {e.Patrocinador}, {e.Miembro});
UPDATE MemberStatistics SET DualTeamPoints = {puntos} WHERE MemberId IN ({e.Raiz}, {e.Patrocinador});
UPDATE DualTeamTree SET LeftLegPoints = {puntos} WHERE MemberId IN ({e.Raiz}, {e.Patrocinador}) AND IsDeleted = 0;
UPDATE MembershipSubscriptions SET DualTeamContribution = 0, EnrollmentContribution = 0 WHERE MemberId = {e.Miembro};");
        db.ChangeTracker.Clear();
    }

    /// <summary>Un pedido completado con los productos de membresia de los niveles dados (nivel 3 = 6 puntos, nivel 2 = 1).</summary>
    private static async Task<string> PedidoAsync(AppDbContext db, string memberId, params int[] niveles)
    {
        var pedido = new OrdersEntity { MemberId = memberId, OrderDate = DateTime.Now, Status = OrderStatus.Completed, CreatedBy = "plan-q-prueba" };
        db.Orders.Add(pedido);
        foreach (var nivel in niveles)
        {
            var producto = await db.Products.AsNoTracking().Where(p => p.MembershipLevelId == nivel)
                .OrderByDescending(p => p.QualificationPoins).Select(p => p.Id).FirstAsync();
            db.OrderDetails.Add(new OrderDetail { OrderId = pedido.Id, ProductId = producto, Quantity = 1, CreatedBy = "plan-q-prueba" });
        }
        await db.SaveChangesAsync();
        return pedido.Id;
    }

    /// <summary>
    /// Revision 2 del bloque 2 (C1): un miembro retenido con puntos de antes de DQ16 y aporte 0 que se reactiva (como hace
    /// RecurringBillingProcessor al cobrar) no suma otra vez: sus puntos ya estan en toda la linea.
    /// </summary>
    [SqlServerFact]
    public Task Reactivar_un_retenido_de_antes_con_puntos_y_aporte_cero_no_cuenta_dos_veces() =>
        ConReintentoAsync(async () =>
        {
            await using var db = SqlServerReportes.Db();
            db.Database.SetCommandTimeout(300);
            await using var tx = await db.Database.BeginTransactionAsync();
            var e = await SembrarAsync(db);
            var seis = await MembershipPointsDeltas.LevelPointsAsync(db, 3);
            var sus = ReportesDb.Suscripcion(db, e.Miembro, 3, MembershipStatus.HoldByBilling, DateTime.Now);
            await db.SaveChangesAsync();
            await VaciarAsync(db);
            await LegadoAsync(db, e, seis);

            await CambiarAsync(db, sus.Id, s => s.SubscriptionStatus = MembershipStatus.Active);
            (await LineaAsync(db, e)).Should().Be((seis, seis, seis, seis, seis, seis), "sus puntos ya estaban en la linea");
            (await db.MembershipSubscriptions.AsNoTracking().SingleAsync(s => s.Id == sus.Id)).DualTeamContribution.Should().Be(seis);

            await CambiarAsync(db, sus.Id, s => s.SubscriptionStatus = MembershipStatus.HoldByBilling);
            (await LineaAsync(db, e)).Should().Be((0m, 0m, 0, 0, 0, 0), "y retenerla de nuevo los resta una vez");
        });

    /// <summary>C1: una pendiente de pago con los puntos que sembraba el alta antiguo, al activarse, no los suma otra vez.</summary>
    [SqlServerFact]
    public Task Activar_una_pendiente_con_puntos_sembrados_no_cuenta_dos_veces() =>
        ConReintentoAsync(async () =>
        {
            await using var db = SqlServerReportes.Db();
            db.Database.SetCommandTimeout(300);
            await using var tx = await db.Database.BeginTransactionAsync();
            var e = await SembrarAsync(db);
            var seis = await MembershipPointsDeltas.LevelPointsAsync(db, 3);
            var sus = ReportesDb.Suscripcion(db, e.Miembro, 3, MembershipStatus.Pending, DateTime.Now);
            await db.SaveChangesAsync();
            await VaciarAsync(db);
            await LegadoAsync(db, e, seis);

            await CambiarAsync(db, sus.Id, s => s.SubscriptionStatus = MembershipStatus.Active);
            (await LineaAsync(db, e)).Should().Be((seis, seis, seis, seis, seis, seis));
        });

    /// <summary>C1: cancelar una retenida de antes de DQ16 deja al miembro en 0 en toda la linea (se corrige en el primer cambio).</summary>
    [SqlServerFact]
    public Task Cancelar_una_retenida_de_antes_quita_sus_puntos_de_la_linea() =>
        ConReintentoAsync(async () =>
        {
            await using var db = SqlServerReportes.Db();
            db.Database.SetCommandTimeout(300);
            await using var tx = await db.Database.BeginTransactionAsync();
            var e = await SembrarAsync(db);
            var seis = await MembershipPointsDeltas.LevelPointsAsync(db, 3);
            var sus = ReportesDb.Suscripcion(db, e.Miembro, 3, MembershipStatus.HoldByBilling, DateTime.Now);
            await db.SaveChangesAsync();
            await VaciarAsync(db);
            await LegadoAsync(db, e, seis);

            await CambiarAsync(db, sus.Id, s => s.SubscriptionStatus = MembershipStatus.Cancelled);
            (await LineaAsync(db, e)).Should().Be((0m, 0m, 0, 0, 0, 0), "sin membresia activa no cuenta");
        });

    /// <summary>
    /// Revision 2 del bloque 2 (I3): la subida sin pedido parte de lo que aportaba la vieja (un pedido de dos productos,
    /// 6 + 1) menos los puntos de su nivel mas los del nivel nuevo: conserva el producto extra en la misma base.
    /// </summary>
    [SqlServerFact]
    public Task Subir_sin_pedido_conserva_los_productos_extra_del_pedido() =>
        ConReintentoAsync(async () =>
        {
            await using var db = SqlServerReportes.Db();
            db.Database.SetCommandTimeout(300);
            await using var tx = await db.Database.BeginTransactionAsync();
            var e = await SembrarAsync(db);
            var p2 = await MembershipPointsDeltas.LevelPointsAsync(db, 2);
            var p3 = await MembershipPointsDeltas.LevelPointsAsync(db, 3);
            var p4 = await MembershipPointsDeltas.LevelPointsAsync(db, 4);
            var pedido = await PedidoAsync(db, e.Miembro, 3, 2);
            var sus = ReportesDb.Suscripcion(db, e.Miembro, 3, MembershipStatus.Active, DateTime.Now);
            sus.LastOrderId = pedido;
            await db.SaveChangesAsync();
            await VaciarAsync(db);
            var siete = p3 + p2;
            (await LineaAsync(db, e)).Should().Be((siete, siete, siete, siete, siete, siete));

            // Como UpgradeMembershipHandler: la vieja caduca y nace otra activa sin pedido, en el mismo guardado.
            var vieja = await db.MembershipSubscriptions.SingleAsync(s => s.Id == sus.Id);
            vieja.SubscriptionStatus = MembershipStatus.Expired;
            var nueva = ReportesDb.Suscripcion(db, e.Miembro, 4, MembershipStatus.Active, DateTime.Now);
            nueva.PreviousMembershipLevelId = 3;
            await db.SaveChangesAsync();
            await VaciarAsync(db);
            var esperado = siete - p3 + p4;
            (await LineaAsync(db, e)).Should().Be((esperado, esperado, esperado, esperado, esperado, esperado));
        });

    /// <summary>Revision 2 del bloque 2 (I4): cambiar de nivel en la misma fila sin pedido nuevo (LastOrderId igual) aplica la diferencia.</summary>
    [SqlServerFact]
    public Task Cambiar_de_nivel_en_la_misma_fila_con_el_pedido_viejo_aplica_la_diferencia() =>
        ConReintentoAsync(async () =>
        {
            await using var db = SqlServerReportes.Db();
            db.Database.SetCommandTimeout(300);
            await using var tx = await db.Database.BeginTransactionAsync();
            var e = await SembrarAsync(db);
            var p2 = await MembershipPointsDeltas.LevelPointsAsync(db, 2);
            var p3 = await MembershipPointsDeltas.LevelPointsAsync(db, 3);
            var pedido = await PedidoAsync(db, e.Miembro, 3, 2);
            var sus = ReportesDb.Suscripcion(db, e.Miembro, 3, MembershipStatus.Active, DateTime.Now);
            sus.LastOrderId = pedido;
            await db.SaveChangesAsync();
            await VaciarAsync(db);

            await CambiarAsync(db, sus.Id, s => { s.MembershipLevelId = 2; s.ChangeReason = SubscriptionChangeReason.Downgrade; });
            var esperado = p3 + p2 - p3 + p2;
            (await LineaAsync(db, e)).Should().Be((esperado, esperado, esperado, esperado, esperado, esperado));
        });

    /// <summary>Un guardado que falla y se reintenta en el mismo contexto aplica los puntos una sola vez.</summary>
    [SqlServerFact]
    public Task Un_guardado_que_falla_y_se_reintenta_cuenta_una_vez() =>
        ConReintentoAsync(async () =>
        {
            await using var db = SqlServerReportes.Db();
            db.Database.SetCommandTimeout(300);
            await using var tx = await db.Database.BeginTransactionAsync();
            var e = await SembrarAsync(db);
            var seis = await MembershipPointsDeltas.LevelPointsAsync(db, 3);
            var sus = ReportesDb.Suscripcion(db, e.Miembro, 3, MembershipStatus.Pending, DateTime.Now);
            await db.SaveChangesAsync();
            await VaciarAsync(db);

            (await db.MembershipSubscriptions.SingleAsync(s => s.Id == sus.Id)).SubscriptionStatus = MembershipStatus.Active;
            // Una fila que rompe la FK hace fallar el guardado entero (EF vuelve a su punto de guardado).
            var mala = ReportesDb.Suscripcion(db, e.Miembro, 987_654, MembershipStatus.Pending, DateTime.Now);
            var fallo = async () => await db.SaveChangesAsync();
            await fallo.Should().ThrowAsync<DbUpdateException>();
            db.Entry(mala).State = EntityState.Detached;
            await db.SaveChangesAsync();
            await VaciarAsync(db);
            (await LineaAsync(db, e)).Should().Be((seis, seis, seis, seis, seis, seis), "el reintento no suma dos veces");
        });

    /// <summary>Activado antes de colocarse: su propio de puntos espera y, al colocarlo, sube una sola vez.</summary>
    [SqlServerFact]
    public Task Activar_antes_de_colocar_cuenta_una_vez() =>
        ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        db.Database.SetCommandTimeout(300);
        await using var tx = await db.Database.BeginTransactionAsync();
        var e = await SembrarAsync(db, colocar: false);
        var seis = await MembershipPointsDeltas.LevelPointsAsync(db, 3);

        ReportesDb.Suscripcion(db, e.Miembro, 3, MembershipStatus.Active, DateTime.Now);
        await db.SaveChangesAsync();
        await VaciarAsync(db);
        (await db.DualLegDeltas.CountAsync(d => d.StartMemberId == e.Miembro && d.OwnChange && d.Points == seis))
            .Should().Be(1, "sin nodo, el propio de puntos espera");

        var p = await db.DualTeamTree.SingleAsync(d => d.MemberId == e.Patrocinador);
        db.DualTeamTree.Add(await DualTeamNodes.ChildAsync(db, p, e.Miembro, TreeSide.Left, DateTime.Now, "plan-q-prueba"));
        await db.SaveChangesAsync();
        await LegPointMaintenance.EnqueueLeafAsync(db, e.Miembro, DateTime.Now, "plan-q-prueba");
        await VaciarAsync(db);
        var r = await LineaAsync(db, e);
        (r.R, r.P).Should().Be((seis, seis), "la hoja lleva lo aplicado (0) y el propio aparcado los puntos");

    });
}
