using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.ConfirmCryptoPayment;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// Un pedido de alta se activa UNA vez, venga el dinero por donde venga (cripto, Entrax, otro QR de
/// Entrax pagado tarde). Lo decide la transición atómica del pedido (Pending/Processing → Completed)
/// que hace SignupOrderClaim dentro de la activación: quien llega segundo no activa nada.
/// En SQL Server es un UPDATE … WHERE Status IN (…) con una fila afectada; EF InMemory no tiene
/// UPDATE condicional y aquí se prueba con un doble que simula haber perdido la carrera.
/// </summary>
public class UnPedidoSeActivaUnaVezTests
{
    /// <summary>Otro camino ya pasó el pedido a Completed entre la lectura y la activación.</summary>
    private sealed class CarreraPerdida : ISignupOrderClaim
    {
        public int Llamadas { get; private set; }

        public Task<bool> TryCompleteAsync(Orders order, DateTime now, string actor, CancellationToken ct)
        {
            Llamadas++;
            return Task.FromResult(false);
        }

        public Task<bool> TryCompleteFromPendingAsync(Orders order, DateTime now, string actor, CancellationToken ct)
        {
            Llamadas++;
            return Task.FromResult(false);
        }

        public Task<bool> TryReleaseAsync(Orders order, DateTime now, string actor, CancellationToken ct)
            => Task.FromResult(true);
    }

    private static SignupActivationService Activacion(
        AppDbContext db, ISignupOrderClaim claim, ISponsorBonusService? sponsor = null)
        => new(db, sponsor ?? Mock.Of<ISponsorBonusService>(), Mock.Of<IFastStartBonusService>(),
               Mock.Of<IRecurringBillingEnrollmentService>(), claim);

    [Theory]
    [InlineData(OrderStatus.Pending, true)]
    [InlineData(OrderStatus.Processing, true)]
    [InlineData(OrderStatus.Completed, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public async Task Solo_un_pedido_pendiente_o_en_proceso_se_puede_completar(OrderStatus estado, bool esperado)
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, estado);

        (await new SignupOrderClaim(db).TryCompleteAsync(
            await db.Orders.SingleAsync(), EscenarioEntrax.Ahora, EscenarioEntrax.Email, default))
            .Should().Be(esperado);
    }

    [Fact]
    public async Task Un_pedido_ya_completado_no_se_vuelve_a_activar()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Completed);
        var sponsor = new Mock<ISponsorBonusService>();

        var r = await Activacion(db, new SignupOrderClaim(db), sponsor.Object)
            .ActivatePendingSignupAsync(await db.Orders.SingleAsync(), EscenarioEntrax.Ahora, default);

        r.ErrorCode.Should().Be(SignupOrderClaim.AlreadySettledCode);
        (await db.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Pending);
        sponsor.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task Si_otra_activacion_gano_el_pedido_no_se_toca_nada_ni_hay_comisiones()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        var sponsor = new Mock<ISponsorBonusService>();
        var claim = new CarreraPerdida();
        var order = await db.Orders.SingleAsync();

        var r = await Activacion(db, claim, sponsor.Object).ActivatePendingSignupAsync(order, EscenarioEntrax.Ahora, default);

        r.ErrorCode.Should().Be(SignupOrderClaim.AlreadySettledCode);
        claim.Llamadas.Should().Be(1);
        order.Status.Should().Be(OrderStatus.Processing);
        (await db.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Pending);
        sponsor.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task El_que_pierde_recibe_ORDER_ALREADY_SETTLED_aunque_el_ganador_ya_activara_la_suscripcion()
    {
        // El ganador confirmó: la suscripción ya no está Pending. Si el pedido no se reclamara primero,
        // el perdedor vería SUBSCRIPTION_NOT_FOUND y lo trataría como otro fallo.
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        (await db.MembershipSubscriptions.SingleAsync()).SubscriptionStatus = MembershipStatus.Active;
        await db.SaveChangesAsync();

        var r = await Activacion(db, new CarreraPerdida())
            .ActivatePendingSignupAsync(await db.Orders.SingleAsync(), EscenarioEntrax.Ahora, default);

        r.ErrorCode.Should().Be(SignupOrderClaim.AlreadySettledCode);
    }

    [Fact]
    public async Task Una_cripto_cancelada_porque_el_alta_se_pago_con_Entrax_tiene_su_propio_codigo()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Completed);
        db.CryptoPaymentConfirmations.Add(new CryptoPaymentConfirmation
        {
            OrderId = EscenarioEntrax.OrderId, MemberId = EscenarioEntrax.MemberId, MemberEmail = EscenarioEntrax.Email,
            CryptoCurrency = "BTC", AmountDue = EscenarioEntrax.Total, Purpose = CryptoPaymentPurpose.Signup,
            Status = CryptoPaymentConfirmationStatus.Cancelled, Notes = "Paid via Entrax (x).",
            CreatedBy = "seed", CreationDate = EscenarioEntrax.Ahora
        });
        await db.SaveChangesAsync();

        var handler = new ConfirmCryptoPaymentHandler(db, EscenarioEntrax.Reloj().Object,
            EscenarioEntrax.Usuarios(EscenarioEntrax.UsuarioInactivo()).Object,
            Activacion(db, new SignupOrderClaim(db)), NullLogger<ConfirmCryptoPaymentHandler>.Instance);
        var r = await handler.Handle(new ConfirmCryptoPaymentCommand(EscenarioEntrax.OrderId,
            new ConfirmCryptoPaymentRequest { CryptoTransactionId = "0xabc" }, "admin-1", "admin@example.com"), default);

        r.ErrorCode.Should().Be(ConfirmCryptoPaymentHandler.CancelledCode,
            "quien confirma tiene que saber que esa transferencia no hay que aplicarla, no que alguien ya la confirmó.");
    }

    [Fact]
    public async Task El_controlador_contesta_409_a_una_cripto_cancelada()
    {
        var mediator = new Mock<MediatR.IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ConfirmCryptoPaymentCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(MLMConquerorGlobalEdition.SharedKernel.Result<ConfirmCryptoPaymentResponse>
                    .Failure(ConfirmCryptoPaymentHandler.CancelledCode, "x"));
        var controller = new MLMConquerorGlobalEdition.SignupAPI.Controllers.SignupsController(
            mediator.Object, Mock.Of<IFraudFingerprintService>())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            }
        };

        var r = await controller.ConfirmCryptoPayment(EscenarioEntrax.OrderId,
            new ConfirmCryptoPaymentRequest { CryptoTransactionId = "0xabc" }, default);

        r.Should().BeOfType<Microsoft.AspNetCore.Mvc.ConflictObjectResult>();
    }

    [Fact]
    public async Task La_cripto_no_confirma_un_pedido_que_otro_pago_activo_en_paralelo()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        db.CryptoPaymentConfirmations.Add(new CryptoPaymentConfirmation
        {
            OrderId = EscenarioEntrax.OrderId, MemberId = EscenarioEntrax.MemberId, MemberEmail = EscenarioEntrax.Email,
            CryptoCurrency = "BTC", AmountDue = EscenarioEntrax.Total, Purpose = CryptoPaymentPurpose.Signup,
            Status = CryptoPaymentConfirmationStatus.AwaitingPayment, CreatedBy = "seed", CreationDate = EscenarioEntrax.Ahora
        });
        await db.SaveChangesAsync();
        var usuario = EscenarioEntrax.UsuarioInactivo();
        var usuarios = EscenarioEntrax.Usuarios(usuario);

        var handler = new ConfirmCryptoPaymentHandler(db, EscenarioEntrax.Reloj().Object, usuarios.Object,
            Activacion(db, new CarreraPerdida()), NullLogger<ConfirmCryptoPaymentHandler>.Instance);
        var r = await handler.Handle(new ConfirmCryptoPaymentCommand(EscenarioEntrax.OrderId,
            new ConfirmCryptoPaymentRequest { CryptoTransactionId = "0xabc" }, "admin-1", "admin@example.com"), default);

        r.ErrorCode.Should().Be("CRYPTO_PAYMENT_ALREADY_CONFIRMED");
        (await db.CryptoPaymentConfirmations.AsNoTracking().SingleAsync()).Status
            .Should().Be(CryptoPaymentConfirmationStatus.AwaitingPayment, "no se guardó nada.");
        usuario.IsActive.Should().BeFalse();
        usuarios.Verify(u => u.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }
}
