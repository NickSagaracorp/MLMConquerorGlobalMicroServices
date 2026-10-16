using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.AdminAPI.Features.Members.UpdateMemberStatus;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Members;

/// <summary>
/// Poner un miembro en Active desde administración es una activación MANUAL y deliberada del personal: pasa la
/// suscripción y el pedido Pending a activos sin cobro. Lo que no puede es pisar un pago en vuelo: si el pedido
/// del alta está en Processing (cripto o Entrax esperando) o hay un pago de Entrax en AwaitingPayment, es
/// <c>409 PAYMENT_IN_FLIGHT</c> y no cambia nada. Ahí lo correcto es confirmar la cripto o hacer Re-check.
/// </summary>
public class ActivacionManualSinPagoEnVueloTests
{
    private static readonly DateTime Ahora = new(2026, 10, 16, 10, 0, 0);
    private const string MemberId = "AMB-PEND";

    private static async Task<AppDbContext> SembrarAsync(OrderStatus estadoDelAlta)
    {
        var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(new MemberProfile
        {
            MemberId = MemberId, FirstName = "Ana", LastName = "Ruiz", Country = "US",
            Status = MemberAccountStatus.Pending, MemberType = MemberType.Ambassador,
            EnrollDate = Ahora, CreationDate = Ahora, LastUpdateDate = Ahora, CreatedBy = "seed"
        });
        db.Orders.Add(new Orders
        {
            Id = "ORD-ALTA", MemberId = MemberId, TotalAmount = 80, Status = estadoDelAlta,
            OrderDate = Ahora, CreatedBy = "seed", CreationDate = Ahora, LastUpdateDate = Ahora
        });
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            MemberId = MemberId, MembershipLevelId = 1, ChangeReason = SubscriptionChangeReason.New,
            SubscriptionStatus = MembershipStatus.Pending, StartDate = Ahora, CreatedBy = "seed", LastUpdateDate = Ahora
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static UpdateMemberStatusHandler Handler(AppDbContext db)
    {
        var usuario = new Mock<ICurrentUserService>();
        usuario.Setup(u => u.UserId).Returns("admin-001");
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(r => r.Now).Returns(Ahora);
        return new UpdateMemberStatusHandler(db, usuario.Object, reloj.Object, new NoOpCacheService());
    }

    private static UpdateMemberStatusCommand Activar()
        => new(MemberId, new UpdateMemberStatusRequest { Status = MemberAccountStatus.Active, Reason = "Pago verificado a mano" });

    private static async Task SinCambiosAsync(AppDbContext db, OrderStatus estadoDelAlta)
    {
        db.ChangeTracker.Clear();
        (await db.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Pending);
        (await db.MembershipSubscriptions.SingleAsync()).SubscriptionStatus.Should().Be(MembershipStatus.Pending);
        (await db.Orders.SingleAsync(o => o.Id == "ORD-ALTA")).Status.Should().Be(estadoDelAlta);
        (await db.MemberStatusHistories.CountAsync()).Should().Be(0, "no hubo cambio de estado");
    }

    [Fact]
    public async Task Con_el_pedido_del_alta_en_Processing_es_PAYMENT_IN_FLIGHT_y_no_cambia_nada()
    {
        await using var db = await SembrarAsync(OrderStatus.Processing);

        var r = await Handler(db).Handle(Activar(), default);

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be("PAYMENT_IN_FLIGHT");
        r.ErrorCode.Should().Be(UpdateMemberStatusHandler.PaymentInFlightCode);
        await SinCambiosAsync(db, OrderStatus.Processing);
    }

    [Fact]
    public async Task Con_un_pago_de_Entrax_esperando_es_PAYMENT_IN_FLIGHT_y_no_cambia_nada()
    {
        await using var db = await SembrarAsync(OrderStatus.Pending);
        db.EntraxPayments.Add(new EntraxPayment
        {
            OrderId = "ORD-ALTA", MemberId = MemberId, ExternalOrderId = EntraxPayment.BuildExternalOrderId("ORD-ALTA", 1),
            Status = EntraxPaymentStatus.AwaitingPayment, AmountUsd = 80,
            CreatedBy = "seed", CreationDate = Ahora, LastUpdateDate = Ahora
        });
        await db.SaveChangesAsync();

        var r = await Handler(db).Handle(Activar(), default);

        r.ErrorCode.Should().Be(UpdateMemberStatusHandler.PaymentInFlightCode);
        await SinCambiosAsync(db, OrderStatus.Pending);
    }

    [Fact]
    public async Task Sin_pago_en_vuelo_la_activacion_manual_sigue_y_queda_en_el_historial()
    {
        await using var db = await SembrarAsync(OrderStatus.Pending);
        db.EntraxPayments.Add(new EntraxPayment
        {
            OrderId = "ORD-ALTA", MemberId = MemberId, ExternalOrderId = EntraxPayment.BuildExternalOrderId("ORD-ALTA", 1),
            Status = EntraxPaymentStatus.Expired, AmountUsd = 80,
            CreatedBy = "seed", CreationDate = Ahora, LastUpdateDate = Ahora
        });
        await db.SaveChangesAsync();

        var r = await Handler(db).Handle(Activar(), default);

        r.IsSuccess.Should().BeTrue();
        db.ChangeTracker.Clear();
        (await db.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Active);
        (await db.MembershipSubscriptions.SingleAsync()).SubscriptionStatus.Should().Be(MembershipStatus.Active);
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Completed);
        var historia = await db.MemberStatusHistories.SingleAsync();
        historia.CreatedBy.Should().Be("admin-001");
        historia.Reason.Should().Be("Pago verificado a mano");
    }

    [Fact]
    public async Task Un_pedido_posterior_en_Processing_no_es_el_del_alta()
    {
        await using var db = await SembrarAsync(OrderStatus.Pending);
        db.Orders.Add(new Orders
        {
            Id = "ORD-RENOV", MemberId = MemberId, TotalAmount = 80, Status = OrderStatus.Processing,
            OrderDate = Ahora.AddMonths(1), CreatedBy = "seed", CreationDate = Ahora, LastUpdateDate = Ahora
        });
        await db.SaveChangesAsync();

        (await Handler(db).Handle(Activar(), default)).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(MemberAccountStatus.Suspended)]
    [InlineData(MemberAccountStatus.Inactive)]
    public async Task Otros_estados_no_miran_los_pagos(MemberAccountStatus estado)
    {
        await using var db = await SembrarAsync(OrderStatus.Processing);

        var r = await Handler(db).Handle(new UpdateMemberStatusCommand(MemberId, new UpdateMemberStatusRequest { Status = estado }), default);

        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task El_controlador_responde_409()
    {
        var mediador = new Mock<IMediator>();
        mediador.Setup(m => m.Send(It.IsAny<UpdateMemberStatusCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<AdminMemberDto>.Failure(UpdateMemberStatusHandler.PaymentInFlightCode, "x"));
        await using var db = InMemoryDbHelper.Create();

        var r = await new MembersController(mediador.Object, db)
            .UpdateMemberStatus(MemberId, new UpdateMemberStatusRequest { Status = MemberAccountStatus.Active });

        var objeto = r.Should().BeAssignableTo<ObjectResult>().Subject;
        objeto.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        objeto.Value.Should().BeOfType<ApiResponse<AdminMemberDto>>().Which.ErrorCode.Should().Be("PAYMENT_IN_FLIGHT");
    }
}
