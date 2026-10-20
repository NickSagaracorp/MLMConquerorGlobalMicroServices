using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// "El dinero del alta está cobrado" en un solo sitio: lo usan la confirmación cripto y Entrax.
/// </summary>
public class ActivarAltaPendienteTests
{
    [Fact]
    public async Task Activa_pedido_miembro_y_suscripcion_y_devuelve_el_miembro()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        var order = await db.Orders.SingleAsync();

        var r = await EscenarioEntrax.Activacion(db).ActivatePendingSignupAsync(order, EscenarioEntrax.Ahora, default);
        await db.SaveChangesAsync();

        r.IsSuccess.Should().BeTrue();
        r.Value!.MemberId.Should().Be(EscenarioEntrax.MemberId);
        order.Status.Should().Be(OrderStatus.Completed);
        (await db.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Active);
        var sub = await db.MembershipSubscriptions.SingleAsync();
        sub.SubscriptionStatus.Should().Be(MembershipStatus.Active);
        sub.StartDate.Should().Be(EscenarioEntrax.Ahora, "el mes arranca cuando el dinero está cobrado.");
    }

    [Fact]
    public async Task Las_comisiones_se_calculan_sobre_el_pedido()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        var sponsor = new Mock<ISponsorBonusService>();
        var activacion = new SignupActivationService(db, sponsor.Object, Mock.Of<IFastStartBonusService>(),
            Mock.Of<IRecurringBillingEnrollmentService>());

        await activacion.ActivatePendingSignupAsync(await db.Orders.SingleAsync(), EscenarioEntrax.Ahora, default);

        sponsor.Verify(s => s.ComputeAsync(null, EscenarioEntrax.MemberId, EscenarioEntrax.OrderId,
            EscenarioEntrax.Total, EscenarioEntrax.Email, EscenarioEntrax.Ahora, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sin_suscripcion_pendiente_no_toca_nada()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        (await db.MembershipSubscriptions.SingleAsync()).SubscriptionStatus = MembershipStatus.Active;
        await db.SaveChangesAsync();
        var order = await db.Orders.SingleAsync();

        var r = await EscenarioEntrax.Activacion(db).ActivatePendingSignupAsync(order, EscenarioEntrax.Ahora, default);

        r.ErrorCode.Should().Be("SUBSCRIPTION_NOT_FOUND");
        order.Status.Should().Be(OrderStatus.Processing);
    }

    [Fact]
    public async Task Sin_miembro_es_MEMBER_NOT_FOUND()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        // Revision del bloque 2 (M-4): sin cascada, las suscripciones del miembro se borran antes.
        db.MembershipSubscriptions.RemoveRange(await db.MembershipSubscriptions.ToListAsync());
        db.MemberProfiles.Remove(await db.MemberProfiles.SingleAsync());
        await db.SaveChangesAsync();

        var r = await EscenarioEntrax.Activacion(db)
            .ActivatePendingSignupAsync(await db.Orders.SingleAsync(), EscenarioEntrax.Ahora, default);

        r.ErrorCode.Should().Be("MEMBER_NOT_FOUND");
    }

    [Fact]
    public async Task Activa_al_usuario_inactivo_y_confirma_el_correo()
    {
        var usuario = EscenarioEntrax.UsuarioInactivo();
        var usuarios = EscenarioEntrax.Usuarios(usuario);

        (await SignupUserActivation.ActivateAsync(usuarios.Object, EscenarioEntrax.Email)).Should().BeTrue();

        usuario.IsActive.Should().BeTrue();
        usuario.EmailConfirmed.Should().BeTrue();
        usuarios.Verify(u => u.UpdateAsync(usuario), Times.Once);
    }

    [Fact]
    public async Task Un_usuario_ya_activo_no_se_toca()
    {
        var usuario = EscenarioEntrax.UsuarioInactivo();
        usuario.IsActive = true;
        var usuarios = EscenarioEntrax.Usuarios(usuario);

        (await SignupUserActivation.ActivateAsync(usuarios.Object, EscenarioEntrax.Email)).Should().BeFalse();
        usuarios.Verify(u => u.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }
}
