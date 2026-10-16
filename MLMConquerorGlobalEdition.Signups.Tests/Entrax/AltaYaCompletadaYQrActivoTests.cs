using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Crypto;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Controllers;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.CompleteSignup;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.SelectProducts;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// Lo que la pantalla del alta necesita saber cuando vuelve a un alta que ya no está en Pending:
///  - completada (por un QR tardío, cripto, tarjeta, token): SIGNUP_ALREADY_SETTLED, en cualquier método
///    y en select-products, no un "alta pendiente no encontrada";
///  - en proceso porque otra ventana tiene un QR de Entrax vivo (o recién pedido): ENTRAX_QR_ALREADY_ACTIVE.
/// </summary>
public class AltaYaCompletadaYQrActivoTests
{
    private static CompleteSignupHandler Handler(AppDbContext db, IEntraxSignupCheckout? checkout = null)
        => new(db, EscenarioEntrax.Reloj().Object, Mock.Of<IS3FileService>(),
               EscenarioEntrax.Usuarios(EscenarioEntrax.UsuarioInactivo()).Object, Mock.Of<IJwtService>(),
               Mock.Of<IEncryptionService>(), Mock.Of<ITokenRedemptionService>(),
               new SignupActivationService(db, Mock.Of<ISponsorBonusService>(), Mock.Of<IFastStartBonusService>(),
                   Mock.Of<IRecurringBillingEnrollmentService>()),
               Direcciones(), new PuertaDeFondosDePrueba(db),
               entraxCheckout: checkout,
               // Encendida a propósito: estas pruebas miran qué pasa con el pedido, no el interruptor
               // de la tarjeta (TarjetaApagadaEnElAltaTests).
               configuration: TarjetaDelAlta.Encendida());

    /// <summary>Direcciones de cripto configuradas: sin ellas el alta con cripto se rechaza antes de mirar el pedido.</summary>
    private static ICryptoDepositAddressReader Direcciones()
    {
        var m = new Mock<ICryptoDepositAddressReader>();
        m.Setup(r => r.GetConfiguredAsync(It.IsAny<CancellationToken>()))
         .ReturnsAsync(new[] { new CryptoDepositAddressDto { Currency = "BTC", Address = "bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh" } });
        return m.Object;
    }

    private static CompleteSignupRequest Pide(PaymentMethodType metodo)
        => new() { PaymentMethod = metodo, CryptoCurrency = metodo == PaymentMethodType.Crypto ? "BTC" : null };

    private static Mock<IEntraxSignupCheckout> Checkout()
    {
        var m = new Mock<IEntraxSignupCheckout>();
        m.Setup(c => c.IsOfferedFor(It.IsAny<string?>())).Returns(true);
        return m;
    }

    private static CompleteSignupRequest Entrax() => new() { PaymentMethod = PaymentMethodType.Entrax };

    private static async Task QrDeOtraVentanaAsync(AppDbContext db, DateTime? expiraEn, EntraxPaymentStatus estado = EntraxPaymentStatus.AwaitingPayment)
    {
        db.EntraxPayments.Add(new EntraxPayment
        {
            OrderId = EscenarioEntrax.OrderId, MemberId = EscenarioEntrax.MemberId,
            ExternalOrderId = EscenarioEntrax.ExternalOrderId(), Status = estado, AmountUsd = EscenarioEntrax.Total,
            ExpiresAt = expiraEn, CreatedBy = "t", CreationDate = EscenarioEntrax.Ahora, LastUpdateDate = EscenarioEntrax.Ahora
        });
        await db.SaveChangesAsync();
    }

    [Theory]
    [InlineData(PaymentMethodType.Entrax)]
    [InlineData(PaymentMethodType.CreditCard)]
    [InlineData(PaymentMethodType.Token)]
    [InlineData(PaymentMethodType.Crypto)]
    public async Task Completar_un_alta_ya_completada_es_SIGNUP_ALREADY_SETTLED_con_cualquier_metodo(PaymentMethodType metodo)
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Completed);
        var checkout = Checkout();

        var r = await Handler(db, checkout.Object)
            .Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, Pide(metodo)), default);

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(CompleteSignupHandler.SignupAlreadySettledCode);
        checkout.Verify(c => c.StartAsync(It.IsAny<Orders>(), It.IsAny<MLMConquerorGlobalEdition.Domain.Entities.Member.MemberProfile>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Un_alta_que_no_existe_sigue_siendo_SIGNUP_NOT_FOUND()
    {
        await using var db = InMemoryDbHelper.Create();

        var r = await Handler(db).Handle(new CompleteSignupCommand("NO-EXISTE", Entrax()), default);

        r.ErrorCode.Should().Be("SIGNUP_NOT_FOUND");
    }

    [Fact]
    public async Task Con_un_QR_vivo_de_otra_ventana_pedir_otro_es_ENTRAX_QR_ALREADY_ACTIVE()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        await QrDeOtraVentanaAsync(db, EscenarioEntrax.Ahora.AddMinutes(10));
        var checkout = Checkout();

        var r = await Handler(db, checkout.Object).Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, Entrax()), default);

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(EntraxSignupErrors.QrAlreadyActive);
        checkout.Verify(c => c.StartAsync(It.IsAny<Orders>(), It.IsAny<MLMConquerorGlobalEdition.Domain.Entities.Member.MemberProfile>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Un_QR_recien_pedido_que_aun_no_tiene_caducidad_tambien_cuenta_como_activo()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        await QrDeOtraVentanaAsync(db, expiraEn: null);

        var r = await Handler(db, Checkout().Object).Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, Entrax()), default);

        r.ErrorCode.Should().Be(EntraxSignupErrors.QrAlreadyActive);
    }

    [Fact]
    public async Task Un_QR_caducado_que_el_servidor_aun_no_cerro_esta_confirmandose_no_perdido()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        await QrDeOtraVentanaAsync(db, EscenarioEntrax.Ahora.AddMinutes(-1));

        var r = await Handler(db, Checkout().Object).Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, Entrax()), default);

        r.ErrorCode.Should().Be(EntraxSignupErrors.PaymentConfirming);
        r.Error.Should().Contain("being confirmed").And.Contain("few minutes");
    }

    [Theory]
    [InlineData(PaymentMethodType.CreditCard)]
    [InlineData(PaymentMethodType.Token)]
    [InlineData(PaymentMethodType.Crypto)]
    public async Task Con_un_QR_vivo_en_otra_ventana_cualquier_metodo_recibe_el_aviso_y_no_un_alta_no_encontrada(PaymentMethodType metodo)
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        await QrDeOtraVentanaAsync(db, EscenarioEntrax.Ahora.AddMinutes(10));

        var r = await Handler(db, Checkout().Object).Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, Pide(metodo)), default);

        r.ErrorCode.Should().Be(EntraxSignupErrors.QrAlreadyActive);
    }

    [Theory]
    [InlineData(PaymentMethodType.CreditCard)]
    [InlineData(PaymentMethodType.Token)]
    [InlineData(PaymentMethodType.Crypto)]
    public async Task Con_un_QR_caducado_sin_cerrar_cualquier_metodo_recibe_el_aviso_de_confirmacion(PaymentMethodType metodo)
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        await QrDeOtraVentanaAsync(db, EscenarioEntrax.Ahora.AddMinutes(-3));

        var r = await Handler(db, Checkout().Object).Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, Pide(metodo)), default);

        r.ErrorCode.Should().Be(EntraxSignupErrors.PaymentConfirming);
    }

    [Fact]
    public async Task Un_pedido_Completed_que_no_es_el_de_un_alta_no_se_confirma_como_alta_pagada()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);
        // Una renovacion posterior del mismo miembro, ya completada: es un pedido, no un alta.
        db.Orders.Add(new Orders
        {
            Id = "ORD-RENOVACION", MemberId = EscenarioEntrax.MemberId, OrderNo = "REN1", TotalAmount = 30m,
            Status = OrderStatus.Completed, OrderDate = EscenarioEntrax.Ahora.AddDays(30), CreatedBy = "t",
            CreationDate = EscenarioEntrax.Ahora, LastUpdateDate = EscenarioEntrax.Ahora
        });
        await db.SaveChangesAsync();

        var completar = await Handler(db, Checkout().Object).Handle(new CompleteSignupCommand("ORD-RENOVACION", Entrax()), default);
        var seleccionar = await new SelectProductsHandler(db, EscenarioEntrax.Reloj().Object)
            .Handle(new SelectProductsCommand("ORD-RENOVACION", new SelectProductsRequest { ProductIds = ["P-RU"] }), default);

        completar.ErrorCode.Should().Be("SIGNUP_NOT_FOUND", "el 409 no puede servir para averiguar si un id de pedido cualquiera existe.");
        seleccionar.ErrorCode.Should().Be("SIGNUP_NOT_FOUND");
    }

    [Fact]
    public async Task Un_QR_pagado_o_fallido_no_cuenta_como_activo()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        await QrDeOtraVentanaAsync(db, EscenarioEntrax.Ahora.AddMinutes(10), EntraxPaymentStatus.Failed);

        var r = await Handler(db, Checkout().Object).Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, Entrax()), default);

        r.ErrorCode.Should().Be("SIGNUP_NOT_FOUND");
    }

    [Fact]
    public async Task Otros_metodos_con_el_pedido_en_proceso_por_cripto_siguen_sin_encontrar_el_alta()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);

        var r = await Handler(db, Checkout().Object).Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, Entrax()), default);

        r.ErrorCode.Should().Be("SIGNUP_NOT_FOUND");
    }

    [Fact]
    public async Task Seleccionar_productos_de_un_alta_completada_es_SIGNUP_ALREADY_SETTLED()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Completed);

        var r = await new SelectProductsHandler(db, EscenarioEntrax.Reloj().Object)
            .Handle(new SelectProductsCommand(EscenarioEntrax.OrderId, new SelectProductsRequest { ProductIds = ["P-RU"] }), default);

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(SignupOrderClaim.SignupAlreadySettledCode);
    }

    [Theory]
    [InlineData(EntraxSignupErrors.QrAlreadyActive, StatusCodes.Status409Conflict)]
    [InlineData(EntraxSignupErrors.PaymentConfirming, StatusCodes.Status409Conflict)]
    [InlineData(SignupOrderClaim.SignupAlreadySettledCode, StatusCodes.Status409Conflict)]
    public async Task El_controlador_responde_409_a_complete_y_a_select_products(string codigo, int http)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CompleteSignupCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<SignupResponse>.Failure(codigo, "x"));
        mediator.Setup(m => m.Send(It.IsAny<SelectProductsCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<bool>.Failure(codigo, "x"));
        var controller = new SignupsController(mediator.Object, Mock.Of<IFraudFingerprintService>());

        (await controller.CompleteSignup(EscenarioEntrax.OrderId, Entrax(), default))
            .Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(http);

        if (codigo == SignupOrderClaim.SignupAlreadySettledCode)
            (await controller.SelectProducts(EscenarioEntrax.OrderId, new SelectProductsRequest { ProductIds = ["P-RU"] }, default))
                .Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(http);
    }
}
