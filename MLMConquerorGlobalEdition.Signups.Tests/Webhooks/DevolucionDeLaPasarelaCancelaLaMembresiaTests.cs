using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Billing;
using MLMConquerorGlobalEdition.SharedAPICenter.DTOs;
using MLMConquerorGlobalEdition.SharedAPICenter.Features.ProcessRefundWebhook;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

using OrdersEntity = MLMConquerorGlobalEdition.Domain.Entities.Orders.Orders;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Webhooks;

/// <summary>
/// Plan Q (Q3, decidido por Nick): una devolucion o un contracargo avisados por la pasarela sobre el pedido vigente de una
/// membresia activa la cancelan (el interceptor resta los puntos) y paran su cobro, con el mismo codigo que la devolucion
/// desde el admin. Una renovacion antigua, un aviso parcial o un OrderId que no es el del pago no la tocan.
/// </summary>
public class DevolucionDeLaPasarelaCancelaLaMembresiaTests
{
    private static readonly DateTime Ahora = new(2026, 10, 20, 9, 30, 0);
    private const string Miembro = "AMB-000001";

    private static async Task<AppDbContext> SembrarAsync()
    {
        var db = InMemoryDbHelper.Create();
        db.Orders.Add(new OrdersEntity { Id = "ord-viejo", MemberId = Miembro, OrderDate = Ahora.AddDays(-40), Status = OrderStatus.Completed, TotalAmount = 100m, CreatedBy = "prueba" });
        db.Orders.Add(new OrdersEntity { Id = "ord-vigente", MemberId = Miembro, OrderDate = Ahora.AddDays(-10), Status = OrderStatus.Completed, TotalAmount = 100m, CreatedBy = "prueba" });
        foreach (var o in new[] { "ord-viejo", "ord-vigente" })
            db.PaymentHistories.Add(new PaymentHistory
            {
                Id = "pago-" + o, OrderId = o, MemberId = Miembro, Amount = 100m, GatewayName = "Spreedly",
                GatewayTransactionId = "tx-" + o, TransactionStatus = PaymentHistoryTransactionStatus.Captured, CreatedBy = "prueba"
            });
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = "sub-1", MemberId = Miembro, MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.Active,
            StartDate = Ahora.AddDays(-40), IsAutoRenew = true, LastOrderId = "ord-vigente", CreatedBy = "prueba"
        });
        db.SubscriptionBillingStates.Add(new SubscriptionBillingState
        {
            Id = "estado-1", MembershipSubscriptionId = "sub-1", MemberId = Miembro, RecurringBillingPlanId = 1,
            BillingAnchorDate = Ahora.AddDays(-40), NextBillingDate = Ahora.AddDays(20), NextAttemptDate = Ahora.AddDays(20),
            Status = RecurringBillingStatus.Active, CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    private static Task<MLMConquerorGlobalEdition.SharedKernel.Result<bool>> AvisarAsync(
        AppDbContext db, string transaccion, decimal importe = 100m, string? pedido = null, string evento = "refund.created")
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(Ahora);
        return new ProcessRefundWebhookHandler(db, reloj.Object).Handle(new ProcessRefundWebhookCommand("stripe",
            new WebhookPaymentPayload { Event = evento, TransactionId = transaccion, Amount = importe, OrderId = pedido }));
    }

    private static async Task<MembershipStatus> EstadoAsync(AppDbContext db) =>
        (await db.MembershipSubscriptions.AsNoTracking().SingleAsync()).SubscriptionStatus;

    [Theory]
    [InlineData("refund.created")]
    [InlineData("chargeback.created")]
    public async Task Devolver_o_contracargar_el_pedido_vigente_cancela_la_membresia_y_para_su_cobro(string evento)
    {
        await using var db = await SembrarAsync();

        var r = await AvisarAsync(db, "tx-ord-vigente", evento: evento);

        r.IsSuccess.Should().BeTrue();
        var sub = await db.MembershipSubscriptions.AsNoTracking().SingleAsync();
        sub.SubscriptionStatus.Should().Be(MembershipStatus.Cancelled);
        sub.CancellationDate.Should().Be(Ahora);
        var estado = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();
        estado.Status.Should().Be(RecurringBillingStatus.Stopped);
        estado.LastFailureReason.Should().Be(MembershipBillingStops.ReasonMembershipRefunded);
    }

    [Fact]
    public async Task Un_aviso_sin_importe_cuenta_como_total()
    {
        await using var db = await SembrarAsync();

        (await AvisarAsync(db, "tx-ord-vigente", importe: 0m)).IsSuccess.Should().BeTrue();

        (await EstadoAsync(db)).Should().Be(MembershipStatus.Cancelled);
    }

    [Fact]
    public async Task Devolver_una_renovacion_antigua_no_toca_la_membresia()
    {
        await using var db = await SembrarAsync();

        (await AvisarAsync(db, "tx-ord-viejo")).IsSuccess.Should().BeTrue();

        (await EstadoAsync(db)).Should().Be(MembershipStatus.Active);
        (await db.SubscriptionBillingStates.AsNoTracking().SingleAsync()).Status.Should().Be(RecurringBillingStatus.Active);
    }

    [Fact]
    public async Task Un_aviso_parcial_no_cancela_la_membresia()
    {
        await using var db = await SembrarAsync();

        (await AvisarAsync(db, "tx-ord-vigente", importe: 40m)).IsSuccess.Should().BeTrue();

        (await EstadoAsync(db)).Should().Be(MembershipStatus.Active);
    }

    [Fact]
    public async Task Un_OrderId_que_no_es_el_del_pago_no_cancela_la_membresia_de_ese_pedido()
    {
        await using var db = await SembrarAsync();

        // El pago es de la renovacion antigua; el aviso dice que es del vigente.
        (await AvisarAsync(db, "tx-ord-viejo", pedido: "ord-vigente")).IsSuccess.Should().BeTrue();

        (await EstadoAsync(db)).Should().Be(MembershipStatus.Active);
    }
}
