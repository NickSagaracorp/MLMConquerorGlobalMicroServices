using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Crypto;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.CompleteSignup;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// El camino inmediato del alta (tarjeta, token, código de descuento) también reclama el pedido de
/// forma atómica, y SOLO desde Pending: si un pago de Entrax tardío (o la cripto) lo activó entre la
/// lectura y la activación, el alta con tarjeta no lo activa otra vez y contesta 409
/// SIGNUP_ALREADY_SETTLED sin guardar nada.
/// </summary>
public class AltaInmediataReclamaElPedidoTests
{
    private static CompleteSignupHandler Handler(
        AppDbContext db, Mock<UserManager<ApplicationUser>> usuarios, ISignupOrderClaim reclamo,
        ISponsorBonusService? sponsor = null, ITokenRedemptionService? canje = null, PuertaDeFondosDePrueba? puerta = null)
        => new(db, EscenarioEntrax.Reloj().Object, Mock.Of<IS3FileService>(), usuarios.Object, Jwt(),
               Mock.Of<IEncryptionService>(), canje ?? Mock.Of<ITokenRedemptionService>(),
               new SignupActivationService(db, sponsor ?? Mock.Of<ISponsorBonusService>(), Mock.Of<IFastStartBonusService>(),
                   Mock.Of<IRecurringBillingEnrollmentService>(), reclamo),
               Mock.Of<ICryptoDepositAddressReader>(), puerta ?? new PuertaDeFondosDePrueba(db),
               orderClaim: reclamo);

    private static IJwtService Jwt()
    {
        var m = new Mock<IJwtService>();
        m.Setup(j => j.GenerateRefreshToken()).Returns("refresh");
        return m.Object;
    }

    private static CompleteSignupCommand ConDescuento()
        => new(EscenarioEntrax.OrderId, new CompleteSignupRequest { PaymentMethod = PaymentMethodType.DiscountCode, DiscountCode = "FREE100" });

    [Fact]
    public async Task Si_gana_el_pedido_se_activa_y_el_reclamo_se_pidio_solo_desde_Pending()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);
        var reclamo = new ReclamoDePrueba(gana: true);

        var r = await Handler(db, EscenarioEntrax.Usuarios(EscenarioEntrax.UsuarioInactivo()), reclamo)
            .Handle(ConDescuento(), default);

        r.IsSuccess.Should().BeTrue();
        reclamo.DesdePending.Should().Be(1);
        reclamo.DesdePendingOProcessing.Should().Be(0, "un pedido en Processing espera otro cobro: la tarjeta no lo completa.");
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Completed);
    }

    [Fact]
    public async Task Si_otro_pago_activo_el_pedido_entre_tanto_es_409_y_no_se_activa_ni_se_guarda_nada()
    {
        var nombre = Guid.NewGuid().ToString();
        await using var db = InMemoryDbHelper.Create(nombre);
        await EscenarioEntrax.SembrarAltaAsync(db);
        var usuario = EscenarioEntrax.UsuarioInactivo();
        var usuarios = EscenarioEntrax.Usuarios(usuario);
        var sponsor = new Mock<ISponsorBonusService>();

        var r = await Handler(db, usuarios, new ReclamoDePrueba(gana: false), sponsor.Object).Handle(ConDescuento(), default);

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(CompleteSignupHandler.SignupAlreadySettledCode);
        r.Value.Should().BeNull("ni sesión ni tokens para un alta que no activó esta llamada.");
        usuario.IsActive.Should().BeFalse();
        usuarios.Verify(u => u.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        sponsor.Verify(s => s.ComputeAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);

        await using var otra = InMemoryDbHelper.Create(nombre);
        (await otra.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Pending);
        (await otra.MembershipSubscriptions.SingleAsync()).SubscriptionStatus.Should().Be(MembershipStatus.Pending);
        (await otra.MemberStatistics.CountAsync()).Should().Be(0, "lo que el alta dejó preparado no se guarda.");
    }

    [Fact]
    public async Task Con_token_el_reclamo_va_dentro_de_la_transaccion_del_candado_y_perder_no_es_TOKEN_NOT_VALID()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);
        db.TokenTransactions.Add(new MLMConquerorGlobalEdition.Domain.Entities.Tokens.TokenTransaction
        {
            ReferenceId = "TOK-1", MemberId = "DUENO-1", CreatedBy = "seed", CreationDate = EscenarioEntrax.Ahora
        });
        await db.SaveChangesAsync();
        var canje = new Mock<ITokenRedemptionService>();
        canje.Setup(c => c.RedeemForSignupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(Result<bool>.Success(true));
        var puerta = new PuertaDeFondosDePrueba(db);
        var reclamo = new ReclamoDePrueba(gana: false, puerta);

        var r = await Handler(db, EscenarioEntrax.Usuarios(EscenarioEntrax.UsuarioInactivo()), reclamo, canje: canje.Object, puerta: puerta)
            .Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId,
                new CompleteSignupRequest { PaymentMethod = PaymentMethodType.Token, TokenCode = "TOK-1" }), default);

        r.ErrorCode.Should().Be(CompleteSignupHandler.SignupAlreadySettledCode);
        reclamo.DentroDelCandado.Should().BeTrue(
            "el reclamo va en la transacción del candado del token: el canje y el pedido se confirman o deshacen juntos.");
    }

    /// <summary>Un reclamo del pedido que gana o pierde a voluntad y anota cómo se le llamó.</summary>
    private sealed class ReclamoDePrueba(bool gana, PuertaDeFondosDePrueba? puerta = null) : ISignupOrderClaim
    {
        public int  DesdePendingOProcessing { get; private set; }
        public int  DesdePending            { get; private set; }
        public bool DentroDelCandado        { get; private set; }

        public Task<bool> TryCompleteAsync(Orders order, DateTime now, string actor, CancellationToken ct)
        {
            DesdePendingOProcessing++;
            return Task.FromResult(gana);
        }

        public Task<bool> TryCompleteFromPendingAsync(Orders order, DateTime now, string actor, CancellationToken ct)
        {
            DesdePending++;
            DentroDelCandado = puerta?.Entrados.Count > 0;
            return Task.FromResult(gana);
        }

        public Task<bool> TryReleaseAsync(Orders order, DateTime now, string actor, CancellationToken ct)
            => Task.FromResult(true);

    }
}
