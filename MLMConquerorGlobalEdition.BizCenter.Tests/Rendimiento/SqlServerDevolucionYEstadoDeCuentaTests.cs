using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Jobs;
using MLMConquerorGlobalEdition.Repository.Services.Billing;

using OrdersEntity = MLMConquerorGlobalEdition.Domain.Entities.Orders.Orders;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>
/// Plan Q (Q3, decidido por Nick): una devolucion o un contracargo del pedido vigente de la membresia la cancela, y
/// suspender, inactivar o terminar la cuenta desde el admin la saca de Active; el interceptor resta los puntos del miembro
/// y de toda su linea (y los devuelve al restaurar las que retuvo el admin). Todo dentro de una transaccion que se deshace.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerDevolucionYEstadoDeCuentaTests
{
    private sealed class Reloj : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private static async Task AplicarAsync(AppDbContext db)
    {
        await new ApplyMemberStatisticDeltasJob(db, new Reloj(), NullLogger<ApplyMemberStatisticDeltasJob>.Instance).ExecuteAsync();
        db.ChangeTracker.Clear();
    }

    private static int Inscripcion(AppDbContext db, string id) =>
        db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == id).OrderBy(s => s.Id).Select(s => s.EnrollmentPoints).FirstOrDefault();

    private static int Personales(AppDbContext db, string id) =>
        db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == id).OrderBy(s => s.Id).Select(s => s.PersonalPoints).FirstOrDefault();

    /// <summary>Un patrocinador con nodo y un miembro debajo, con estadisticas a 0. Devuelve [patrocinador, miembro].</summary>
    private static async Task<string[]> LineaAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("UPDATE MemberStatisticDeltas SET IsApplied = 1 WHERE IsApplied = 0");
        var p = SqlServerReportes.NuevoId("DV");
        var m = SqlServerReportes.NuevoId("DV");
        ReportesDb.Miembro(db, p);
        ReportesDb.Nodo(db, p, null, ReportesDb.Ruta(p), 0);
        ReportesDb.Puntos(db, p, 0);
        ReportesDb.Miembro(db, m, p);
        ReportesDb.Nodo(db, m, p, ReportesDb.Ruta(p, m), 1);
        ReportesDb.Puntos(db, m, 0);
        await db.SaveChangesAsync();
        return new[] { p, m };
    }

    /// <summary>Un pedido completado del miembro con el producto real de membresia de mas puntos del nivel.</summary>
    private static async Task<(OrdersEntity Pedido, int Puntos)> PedidoAsync(AppDbContext db, string m, int nivel)
    {
        var producto = await db.Products.AsNoTracking().Where(x => x.MembershipLevelId == nivel && x.QualificationPoins > 0)
            .OrderByDescending(x => x.QualificationPoins).FirstAsync();
        var pedido = new OrdersEntity
        {
            MemberId = m, OrderDate = DateTime.Now, Status = OrderStatus.Completed, TotalAmount = 100m, CreatedBy = "prueba"
        };
        db.Orders.Add(pedido);
        db.OrderDetails.Add(new OrderDetail { OrderId = pedido.Id, ProductId = producto.Id, Quantity = 1, CreatedBy = "prueba" });
        await db.SaveChangesAsync();
        return (pedido, producto.QualificationPoins);
    }

    private static async Task<MembershipSubscription> ActivaAsync(AppDbContext db, string m, string pedidoId)
    {
        var s = ReportesDb.Suscripcion(db, m, 3, MembershipStatus.Active, DateTime.Now);
        s.LastOrderId = pedidoId;
        s.IsAutoRenew = true;
        await db.SaveChangesAsync();
        await AplicarAsync(db);
        return s;
    }

    private static async Task<SubscriptionBillingState> EstadoDeCobroAsync(AppDbContext db, MembershipSubscription s)
    {
        var plan = await db.RecurringBillingPlans.AsNoTracking().FirstAsync();
        var estado = new SubscriptionBillingState
        {
            MembershipSubscriptionId = s.Id, MemberId = s.MemberId, RecurringBillingPlanId = plan.Id,
            BillingAnchorDate = DateTime.Now.Date, NextBillingDate = DateTime.Now.Date.AddDays(10),
            NextAttemptDate = DateTime.Now.Date.AddDays(10), Status = RecurringBillingStatus.Active, CreatedBy = "prueba"
        };
        db.SubscriptionBillingStates.Add(estado);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return estado;
    }

    [SqlServerFact]
    public async Task Devolver_el_pedido_vigente_cancela_la_membresia_y_resta_los_puntos_del_miembro_y_su_linea()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var ids = await LineaAsync(db);
        var (pedido, puntos) = await PedidoAsync(db, ids[1], 3);
        var s = await ActivaAsync(db, ids[1], pedido.Id);
        new[] { Personales(db, ids[1]), Inscripcion(db, ids[1]), Inscripcion(db, ids[0]) }.Should().Equal(puntos, puntos, puntos);
        var estado = await EstadoDeCobroAsync(db, s);

        var ahora = DateTime.Now;
        var cancelada = await RefundedMembershipOrders.CancelSubscriptionAsync(db, pedido.Id, ahora, "prueba");
        await db.SaveChangesAsync();
        await AplicarAsync(db);

        cancelada.Should().NotBeNull();
        var guardada = await db.MembershipSubscriptions.AsNoTracking().SingleAsync(x => x.Id == s.Id);
        guardada.SubscriptionStatus.Should().Be(MembershipStatus.Cancelled);
        guardada.CancellationDate.Should().Be(ahora);
        guardada.IsAutoRenew.Should().BeFalse();
        new[] { Personales(db, ids[1]), Inscripcion(db, ids[1]), Inscripcion(db, ids[0]) }.Should().Equal(0, 0, 0);
        var parado = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync(x => x.Id == estado.Id);
        parado.Status.Should().Be(RecurringBillingStatus.Stopped);
        parado.LastFailureReason.Should().Be(MembershipBillingStops.ReasonMembershipRefunded);
    }

    [SqlServerFact]
    public async Task Devolver_una_renovacion_antigua_no_cambia_nada()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var ids = await LineaAsync(db);
        var (viejo, _) = await PedidoAsync(db, ids[1], 3);
        var (vigente, puntos) = await PedidoAsync(db, ids[1], 3);
        var s = await ActivaAsync(db, ids[1], vigente.Id);

        var cancelada = await RefundedMembershipOrders.CancelSubscriptionAsync(db, viejo.Id, DateTime.Now, "prueba");
        await db.SaveChangesAsync();
        await AplicarAsync(db);

        cancelada.Should().BeNull();
        (await db.MembershipSubscriptions.AsNoTracking().SingleAsync(x => x.Id == s.Id)).SubscriptionStatus
            .Should().Be(MembershipStatus.Active);
        new[] { Personales(db, ids[1]), Inscripcion(db, ids[1]), Inscripcion(db, ids[0]) }.Should().Equal(puntos, puntos, puntos);
    }

    [SqlServerFact]
    public async Task Suspender_retiene_y_resta_y_reactivar_devuelve_solo_las_que_retuvo_el_admin()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var ids = await LineaAsync(db);
        var (pedido, puntos) = await PedidoAsync(db, ids[1], 3);
        var s = await ActivaAsync(db, ids[1], pedido.Id);
        var estado = await EstadoDeCobroAsync(db, s);
        // Otra suya retenida por otro motivo (sin la marca del admin).
        var otra = ReportesDb.Suscripcion(db, ids[1], 1, MembershipStatus.OnHold, DateTime.Now.AddMonths(-3));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var ahora = DateTime.Now;
        (await AccountStatusMemberships.HoldAsync(db, ids[1], MemberAccountStatus.Suspended, ahora, "admin")).Should().Be(1);
        await db.SaveChangesAsync();
        await AplicarAsync(db);

        var retenida = await db.MembershipSubscriptions.AsNoTracking().SingleAsync(x => x.Id == s.Id);
        retenida.SubscriptionStatus.Should().Be(MembershipStatus.OnHold);
        retenida.HoldDate.Should().Be(ahora);
        retenida.HeldByAccountStatus.Should().Be(MemberAccountStatus.Suspended);
        new[] { Personales(db, ids[1]), Inscripcion(db, ids[1]), Inscripcion(db, ids[0]) }.Should().Equal(0, 0, 0);
        var parado = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync(x => x.Id == estado.Id);
        parado.Status.Should().Be(RecurringBillingStatus.Stopped);
        parado.LastFailureReason.Should().Be(MembershipBillingStops.ReasonMembershipOnHold);

        (await AccountStatusMemberships.RestoreAsync(db, ids[1], ahora.AddMinutes(1), "admin")).Should().Be(1);
        await db.SaveChangesAsync();
        await AplicarAsync(db);

        var restaurada = await db.MembershipSubscriptions.AsNoTracking().SingleAsync(x => x.Id == s.Id);
        restaurada.SubscriptionStatus.Should().Be(MembershipStatus.Active);
        restaurada.HeldByAccountStatus.Should().BeNull();
        restaurada.HoldDate.Should().BeNull();
        (await db.MembershipSubscriptions.AsNoTracking().SingleAsync(x => x.Id == otra.Id)).SubscriptionStatus
            .Should().Be(MembershipStatus.OnHold, "la retenida por otro motivo no la reactiva el admin");
        new[] { Personales(db, ids[1]), Inscripcion(db, ids[1]), Inscripcion(db, ids[0]) }.Should().Equal(puntos, puntos, puntos);
        var reanudado = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync(x => x.Id == estado.Id);
        reanudado.Status.Should().Be(RecurringBillingStatus.Active);
        reanudado.LastFailureReason.Should().BeNull();
    }

    [SqlServerFact]
    public async Task Terminar_cancela_las_activas_y_las_retenidas_por_el_admin_y_resta_los_puntos()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var ids = await LineaAsync(db);
        var (pedido, puntos) = await PedidoAsync(db, ids[1], 3);
        var s = await ActivaAsync(db, ids[1], pedido.Id);
        var otra = ReportesDb.Suscripcion(db, ids[1], 1, MembershipStatus.OnHold, DateTime.Now.AddMonths(-3));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Personales(db, ids[1]).Should().Be(puntos);

        var ahora = DateTime.Now;
        (await AccountStatusMemberships.CancelForTerminationAsync(db, ids[1], ahora, "admin")).Should().Be(1);
        await db.SaveChangesAsync();
        await AplicarAsync(db);

        var cancelada = await db.MembershipSubscriptions.AsNoTracking().SingleAsync(x => x.Id == s.Id);
        cancelada.SubscriptionStatus.Should().Be(MembershipStatus.Cancelled);
        cancelada.CancellationDate.Should().Be(ahora);
        (await db.MembershipSubscriptions.AsNoTracking().SingleAsync(x => x.Id == otra.Id)).SubscriptionStatus
            .Should().Be(MembershipStatus.OnHold, "una retenida por otro motivo se queda como esta");
        new[] { Personales(db, ids[1]), Inscripcion(db, ids[1]), Inscripcion(db, ids[0]) }.Should().Equal(0, 0, 0);
    }
}
