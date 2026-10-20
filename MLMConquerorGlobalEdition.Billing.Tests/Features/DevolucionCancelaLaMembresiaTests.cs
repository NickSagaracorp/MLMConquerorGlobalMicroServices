using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MLMConquerorGlobalEdition.Billing.DTOs;
using MLMConquerorGlobalEdition.Billing.Features.Refund;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Billing;
using MLMConquerorGlobalEdition.SharedKernel;

using OrdersEntity = MLMConquerorGlobalEdition.Domain.Entities.Orders.Orders;

namespace MLMConquerorGlobalEdition.Billing.Tests.Features;

/// <summary>
/// Plan Q (Q3, decidido por Nick): la devolucion total desde el admin del pedido vigente de una membresia activa la cancela
/// (el interceptor resta los puntos) y para su cobro. Una renovacion antigua o una devolucion parcial no la tocan.
/// </summary>
public class DevolucionCancelaLaMembresiaTests
{
    private static readonly DateTime Ahora = new(2026, 10, 20, 9, 30, 0);
    private const string Miembro = "AMB-000001";

    private static async Task<AppDbContext> SembrarAsync(string pedidoVigente)
    {
        var db = TestDbContextFactory.Create();
        db.Orders.Add(new OrdersEntity { Id = "ord-viejo", MemberId = Miembro, OrderDate = Ahora.AddDays(-40), Status = OrderStatus.Completed, TotalAmount = 100m, CreatedBy = "prueba" });
        db.Orders.Add(new OrdersEntity { Id = "ord-vigente", MemberId = Miembro, OrderDate = Ahora.AddDays(-10), Status = OrderStatus.Completed, TotalAmount = 100m, CreatedBy = "prueba" });
        foreach (var o in new[] { "ord-viejo", "ord-vigente" })
            db.PaymentHistories.Add(new PaymentHistory
            {
                Id = "pago-" + o, OrderId = o, MemberId = Miembro, Amount = 100m, GatewayName = nameof(WalletType.Dwolla),
                GatewayTransactionId = "tx-" + o, TransactionStatus = PaymentHistoryTransactionStatus.Captured, CreatedBy = "prueba"
            });
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = "sub-1", MemberId = Miembro, MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.Active,
            StartDate = Ahora.AddDays(-40), IsAutoRenew = true, LastOrderId = pedidoVigente, CreatedBy = "prueba"
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

    private static RefundHandler Handler(AppDbContext db)
    {
        var pasarela = new Mock<IGatewayService>();
        pasarela.Setup(g => g.RefundAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(true));
        var resolutor = new Mock<IGatewayResolver>();
        resolutor.Setup(r => r.Resolve(It.IsAny<WalletType>())).Returns(pasarela.Object);
        var usuario = new Mock<ICurrentUserService>();
        usuario.Setup(u => u.UserId).Returns("admin-1");
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(r => r.Now).Returns(Ahora);
        return new RefundHandler(db, resolutor.Object, usuario.Object, reloj.Object);
    }

    private static Task<Result<bool>> DevolverAsync(AppDbContext db, string pedido, decimal? importe = null) =>
        Handler(db).Handle(new RefundCommand(new RefundRequest { PaymentHistoryId = "pago-" + pedido, Amount = importe, Reason = "prueba" }),
            CancellationToken.None);

    [Fact]
    public async Task Devolver_el_pedido_vigente_cancela_la_membresia_y_para_su_cobro()
    {
        await using var db = await SembrarAsync("ord-vigente");

        var r = await DevolverAsync(db, "ord-vigente");

        r.IsSuccess.Should().BeTrue();
        var sub = await db.MembershipSubscriptions.AsNoTracking().SingleAsync();
        sub.SubscriptionStatus.Should().Be(MembershipStatus.Cancelled);
        sub.CancellationDate.Should().Be(Ahora);
        sub.IsAutoRenew.Should().BeFalse();
        var estado = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();
        estado.Status.Should().Be(RecurringBillingStatus.Stopped);
        estado.LastFailureReason.Should().Be(MembershipBillingStops.ReasonMembershipRefunded);
        (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == "ord-vigente")).Status.Should().Be(OrderStatus.Refunded);
    }

    [Fact]
    public async Task Devolver_una_renovacion_antigua_no_toca_la_membresia()
    {
        await using var db = await SembrarAsync("ord-vigente");

        var r = await DevolverAsync(db, "ord-viejo");

        r.IsSuccess.Should().BeTrue();
        (await db.MembershipSubscriptions.AsNoTracking().SingleAsync()).SubscriptionStatus.Should().Be(MembershipStatus.Active);
        (await db.SubscriptionBillingStates.AsNoTracking().SingleAsync()).Status.Should().Be(RecurringBillingStatus.Active);
        (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == "ord-viejo")).Status.Should().Be(OrderStatus.Refunded);
    }

    [Fact]
    public async Task Una_devolucion_parcial_no_cancela_la_membresia()
    {
        await using var db = await SembrarAsync("ord-vigente");

        var r = await DevolverAsync(db, "ord-vigente", 40m);

        r.IsSuccess.Should().BeTrue();
        (await db.MembershipSubscriptions.AsNoTracking().SingleAsync()).SubscriptionStatus.Should().Be(MembershipStatus.Active);
    }

    [Fact]
    public async Task Una_devolucion_por_el_importe_entero_cancela_igual_que_sin_importe()
    {
        await using var db = await SembrarAsync("ord-vigente");

        var r = await DevolverAsync(db, "ord-vigente", 100m);

        r.IsSuccess.Should().BeTrue();
        (await db.MembershipSubscriptions.AsNoTracking().SingleAsync()).SubscriptionStatus.Should().Be(MembershipStatus.Cancelled);
    }
}
