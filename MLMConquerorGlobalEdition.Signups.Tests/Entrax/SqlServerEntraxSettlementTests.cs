using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// Las carreras que EF InMemory no puede probar, contra SQL Server de verdad: el UPDATE condicional del
/// pedido (dos transacciones a la vez), su fallo cerrado fuera de transacción, la vuelta condicional a
/// Pending y la liquidación simultánea del MISMO pago por el webhook y la pantalla. Cada prueba usa un
/// miembro y un pedido sintéticos propios y borra lo que escribió, pase o falle. Se saltan sin servidor.
/// Necesitan la migración EntraxEnElAlta aplicada a la base de pruebas.
/// </summary>
public class SqlServerEntraxSettlementTests : IAsyncLifetime
{
    // MemberProfiles.MemberId y MembershipSubscriptions.MemberId son nvarchar(20).
    private readonly string _member = $"TST-{Guid.NewGuid():N}"[..20];
    private readonly string _order  = $"ORD-{Guid.NewGuid():N}"[..24];
    private string Email => $"{_member}@example.com";

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (!SqlServerTestDb.Reachable) return;

        // La base de desarrollo es compartida: un DELETE puede chocar en un interbloqueo (1205) con un
        // trabajo que recorre las mismas tablas. Prioridad alta y, si aun así sale víctima, se repite.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await CleanUpOnceAsync();
                return;
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 1205 && attempt < 8)
            {
                await Task.Delay(500 * attempt);
            }
        }
    }

    private async Task CleanUpOnceAsync()
    {
        await using var db = SqlServerTestDb.Db();
        var m = _member;
        var o = _order;
        var prefix = $"SIGNUP-{o}-%";
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            SET DEADLOCK_PRIORITY HIGH;
            DELETE FROM EntraxApiCalls WHERE ExternalOrderId LIKE {prefix};
            DELETE FROM EntraxPayments WHERE OrderId = {o};
            DELETE FROM PaymentHistories WHERE OrderId = {o};
            DELETE FROM CryptoPaymentConfirmations WHERE OrderId = {o};
            DELETE FROM MemberStatisticDeltas WHERE SourceMemberId = {m};
            DELETE FROM MembershipSubscriptions WHERE MemberId = {m};
            DELETE FROM Orders WHERE Id = {o};
            DELETE FROM MemberProfiles WHERE MemberId = {m};");
    }

    private async Task SembrarAsync(OrderStatus pedido = OrderStatus.Processing)
    {
        await using var db = SqlServerTestDb.Db();
        var now = DateTime.Now;
        var level = await db.MembershipLevels.AsNoTracking().OrderBy(l => l.Id).Select(l => l.Id).FirstAsync();

        db.MemberProfiles.Add(new MemberProfile
        {
            // UserId propio: el índice único de MemberProfiles.UserId choca si dos sintéticos dejan el Guid vacío.
            MemberId = _member, UserId = Guid.NewGuid(), Email = Email, FirstName = "Prueba", LastName = "SQL",
            DateOfBirth = new DateTime(1990, 1, 1), MemberType = MemberType.Ambassador, Status = MemberAccountStatus.Pending,
            EnrollDate = now, Country = "RU", CreatedBy = "sql-test", CreationDate = now, LastUpdateDate = now
        });
        db.Orders.Add(new Orders
        {
            Id = _order, MemberId = _member, TotalAmount = 80m, Status = pedido, OrderDate = now,
            CreatedBy = "sql-test", CreationDate = now, LastUpdateDate = now
        });
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = Guid.NewGuid().ToString(), MemberId = _member, MembershipLevelId = level,
            ChangeReason = SubscriptionChangeReason.New, SubscriptionStatus = MembershipStatus.Pending,
            StartDate = now, IsAutoRenew = true, CreatedBy = "sql-test", CreationDate = now, LastUpdateDate = now
        });
        await db.SaveChangesAsync();
    }

    private async Task<string> SembrarPagoAsync(
        int intento, string paymentId, EntraxPaymentStatus estado = EntraxPaymentStatus.AwaitingPayment, bool consumido = false)
    {
        await using var db = SqlServerTestDb.Db();
        var now = DateTime.Now;
        var p = new EntraxPayment
        {
            OrderId = _order, MemberId = _member, ExternalOrderId = EntraxPayment.BuildExternalOrderId(_order, intento),
            ExternalPaymentId = paymentId, Status = estado, Consumed = consumido, AmountUsd = 80m,
            ExpiresAt = now.AddMinutes(15), CreatedBy = "sql-test", CreationDate = now, LastUpdateDate = now
        };
        db.EntraxPayments.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    // ── El pedido ────────────────────────────────────────────────────────────────

    [SqlServerFact]
    public async Task Dos_reclamos_simultaneos_del_mismo_pedido_solo_uno_gana()
    {
        await SembrarAsync();
        await using var dbA = SqlServerTestDb.Db();
        await using var dbB = SqlServerTestDb.Db();

        // Los dos leen el pedido en Processing antes de que nadie lo reclame.
        var orderA = await dbA.Orders.SingleAsync(o => o.Id == _order);
        var orderB = await dbB.Orders.SingleAsync(o => o.Id == _order);

        await using var txA = await dbA.Database.BeginTransactionAsync();
        var ganaA = await new SignupOrderClaim(dbA).TryCompleteAsync(orderA, DateTime.Now, "a", default);

        await using var txB = await dbB.Database.BeginTransactionAsync();
        var reclamoB = new SignupOrderClaim(dbB).TryCompleteAsync(orderB, DateTime.Now, "b", default);
        await Task.Delay(1000);
        reclamoB.IsCompleted.Should().BeFalse("B espera al bloqueo de la fila que tiene A.");

        await txA.CommitAsync();
        var ganaB = await reclamoB.WaitAsync(TimeSpan.FromSeconds(30));
        await txB.CommitAsync();

        ganaA.Should().BeTrue();
        ganaB.Should().BeFalse("cuando A confirma, la fila ya no está ni en Pending ni en Processing.");
        await using var db = SqlServerTestDb.Db();
        (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == _order)).Status.Should().Be(OrderStatus.Completed);
    }

    [SqlServerFact]
    public async Task Reclamar_el_pedido_fuera_de_una_transaccion_lanza_y_no_lo_toca()
    {
        await SembrarAsync();
        await using var db = SqlServerTestDb.Db();
        var order = await db.Orders.SingleAsync(o => o.Id == _order);

        var accion = () => new SignupOrderClaim(db).TryCompleteAsync(order, DateTime.Now, "x", default);

        await accion.Should().ThrowAsync<InvalidOperationException>();
        (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == _order)).Status.Should().Be(OrderStatus.Processing);
    }

    [SqlServerFact]
    public async Task La_vuelta_a_Pending_no_pisa_un_pedido_que_otro_camino_completo()
    {
        await SembrarAsync();
        await using var db = SqlServerTestDb.Db();
        var leido = await db.Orders.SingleAsync(o => o.Id == _order);   // Processing

        await using (var otro = SqlServerTestDb.Db())
        {
            (await otro.Orders.SingleAsync(o => o.Id == _order)).Status = OrderStatus.Completed;
            await otro.SaveChangesAsync();
        }

        (await new SignupOrderClaim(db).TryReleaseAsync(leido, DateTime.Now, "x", default)).Should().BeFalse();
        await db.SaveChangesAsync();

        await using var check = SqlServerTestDb.Db();
        (await check.Orders.AsNoTracking().SingleAsync(o => o.Id == _order)).Status.Should().Be(OrderStatus.Completed);
    }

    // ── El mismo pago por dos caminos ────────────────────────────────────────────

    [SqlServerFact]
    public async Task El_webhook_y_la_pantalla_liquidan_el_mismo_pago_a_la_vez_y_no_hay_falso_doble_cobro()
    {
        await SembrarAsync();
        var paymentId = Guid.NewGuid().ToString("D");
        var pagoId = await SembrarPagoAsync(1, paymentId);

        // Los dos preguntan a Entrax y siguen a la vez.
        var llegados = 0;
        var juntos = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cliente = new Mock<IEntraxClient>();
        cliente.Setup(c => c.GetPaymentAsync(paymentId, It.IsAny<CancellationToken>()))
               .Returns(async () =>
               {
                   if (Interlocked.Increment(ref llegados) >= 2) juntos.TrySetResult();
                   await juntos.Task.WaitAsync(TimeSpan.FromSeconds(10));
                   return EntraxCall<EntraxPaymentState>.Success(new EntraxPaymentState(paymentId, "paid",
                       EntraxPayment.BuildExternalOrderId(_order, 1), 80m, "USD", 7640m, 95.5m, DateTimeOffset.Now), 200, 1);
               });

        await using var dbA = SqlServerTestDb.Db();
        await using var dbB = SqlServerTestDb.Db();
        var resultados = await Task.WhenAll(
            Liquidacion(dbA, cliente.Object).SettleAsync(pagoId, EntraxSettlementSource.Webhook, default),
            Liquidacion(dbB, cliente.Object).SettleAsync(pagoId, EntraxSettlementSource.Poll, default));

        resultados.Should().BeEquivalentTo(new[] { EntraxSettlementOutcome.Activated, EntraxSettlementOutcome.AlreadyResolved });
        await using var db = SqlServerTestDb.Db();
        var pago = await db.EntraxPayments.AsNoTracking().SingleAsync(p => p.Id == pagoId);
        pago.Status.Should().Be(EntraxPaymentStatus.Paid, "un pago legítimo liquidado dos veces a la vez no pasa a revisar.");
        pago.Consumed.Should().BeTrue();
        pago.FailureCode.Should().BeNull();
        (await db.PaymentHistories.AsNoTracking().CountAsync(h => h.OrderId == _order)).Should().Be(1);
        (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == _order)).Status.Should().Be(OrderStatus.Completed);
        (await db.MemberProfiles.AsNoTracking().SingleAsync(m => m.MemberId == _member)).Status.Should().Be(MemberAccountStatus.Active);
    }

    [SqlServerFact]
    public async Task Si_otro_pago_del_pedido_ya_esta_consumido_el_indice_unico_lo_convierte_en_doble_cobro()
    {
        // El pedido sigue en Processing (así la transición del pedido no lo frena) pero otro QR del mismo
        // pedido ya está consumido: lo que salta es UX_EntraxPayments_OrderId_Consumed (2601) al reclamar
        // este pago. Se reconoce por el número de error de SQL Server, no por el texto del mensaje.
        await SembrarAsync();
        await SembrarPagoAsync(1, Guid.NewGuid().ToString("D"), EntraxPaymentStatus.Expired, consumido: true);
        var paymentId = Guid.NewGuid().ToString("D");
        var pagoId = await SembrarPagoAsync(2, paymentId);
        var cliente = new Mock<IEntraxClient>();
        cliente.Setup(c => c.GetPaymentAsync(paymentId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(EntraxCall<EntraxPaymentState>.Success(new EntraxPaymentState(paymentId, "paid",
                   EntraxPayment.BuildExternalOrderId(_order, 2), 80m, "USD", 7640m, 95.5m, DateTimeOffset.Now), 200, 1));

        await using var dbA = SqlServerTestDb.Db();
        var r = await Liquidacion(dbA, cliente.Object).SettleAsync(pagoId, EntraxSettlementSource.Sweep, default);

        r.Should().Be(EntraxSettlementOutcome.ToReview);
        await using var db = SqlServerTestDb.Db();
        var pago = await db.EntraxPayments.AsNoTracking().SingleAsync(p => p.Id == pagoId);
        pago.FailureCode.Should().Be(SignupOrderClaim.AlreadySettledCode);
        pago.Consumed.Should().BeFalse();
        (await db.PaymentHistories.AsNoTracking().CountAsync(h => h.OrderId == _order)).Should().Be(0);
        (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == _order)).Status.Should().Be(OrderStatus.Processing);
    }

    [SqlServerFact]
    public async Task Dos_pantallas_a_la_vez_solo_una_pregunta_a_Entrax()
    {
        // El turno de consulta se reserva con un UPDATE condicional sobre LastCheckedAt: con dos
        // pestañas abiertas (o dos peticiones) en el mismo instante, solo una llama a Entrax.
        await SembrarAsync();
        var pagoId = await SembrarPagoAsync(1, Guid.NewGuid().ToString("D"));
        var liquidacion = new Mock<IEntraxSignupSettlement>();
        liquidacion.Setup(s => s.SettleAsync(pagoId, EntraxSettlementSource.Poll, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(EntraxSettlementOutcome.StillPending);
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(c => c.Now).Returns(() => DateTime.Now);

        await using var dbA = SqlServerTestDb.Db();
        await using var dbB = SqlServerTestDb.Db();
        EntraxSignupStatusService Servicio(AppDbContext db) => new(db, liquidacion.Object, new EntraxOptions(),
            reloj.Object, NullLogger<EntraxSignupStatusService>.Instance);

        await Task.WhenAll(Servicio(dbA).GetAsync(_order, pagoId, default), Servicio(dbB).GetAsync(_order, pagoId, default));

        liquidacion.Verify(s => s.SettleAsync(pagoId, EntraxSettlementSource.Poll, It.IsAny<CancellationToken>()), Times.Once);
    }

    private EntraxSignupSettlement Liquidacion(AppDbContext db, IEntraxClient cliente)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(c => c.Now).Returns(() => DateTime.Now);
        reloj.Setup(c => c.UtcNow).Returns(() => DateTime.UtcNow);

        var usuarios = UserManagerHelper.Create();
        usuarios.Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(new ApplicationUser { Email = Email, UserName = Email, IsActive = false });
        usuarios.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);

        var activacion = new SignupActivationService(db, Mock.Of<ISponsorBonusService>(), Mock.Of<IFastStartBonusService>(),
            Mock.Of<IRecurringBillingEnrollmentService>(), new SignupOrderClaim(db));
        return new EntraxSignupSettlement(db, cliente, activacion, usuarios.Object, reloj.Object,
            NullLogger<EntraxSignupSettlement>.Instance, new SignupOrderClaim(db));
    }
}
