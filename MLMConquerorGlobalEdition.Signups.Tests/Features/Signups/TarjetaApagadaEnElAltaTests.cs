using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Crypto;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Controllers;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.CompleteSignup;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Signups;

/// <summary>
/// La tarjeta del alta va detrás de <c>Signup:CardEnabled</c>, apagada por defecto y si falta. Hoy la
/// tokenización del navegador es simulada y el gateway de tarjeta un stub: nada cobra. Con el interruptor
/// apagado, completar el alta con tarjeta es <c>400 CARD_PAYMENTS_DISABLED</c> y no toca nada: ni activa,
/// ni guarda la tarjeta, ni reclama el pedido, que sigue en Pending para pagar con otro método.
/// </summary>
public class TarjetaApagadaEnElAltaTests
{
    private sealed record Montaje(
        CompleteSignupHandler Handler, Mock<IS3FileService> S3, Mock<ISponsorBonusService> Patrocinio);

    private static Montaje Montar(AppDbContext db, IConfiguration? config)
    {
        var s3 = new Mock<IS3FileService>();
        var patrocinio = new Mock<ISponsorBonusService>();
        var direcciones = new Mock<ICryptoDepositAddressReader>();
        direcciones.Setup(r => r.GetConfiguredAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new[] { new CryptoDepositAddressDto { Currency = "BTC", Address = "bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh" } });
        var jwt = new Mock<IJwtService>();
        jwt.Setup(j => j.GenerateAccessToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>()))
           .Returns("access");
        jwt.Setup(j => j.GenerateRefreshToken()).Returns("refresh");
        var cifrado = new Mock<IEncryptionService>();
        cifrado.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(v => "enc:" + v);

        var handler = new CompleteSignupHandler(
            db, EscenarioEntrax.Reloj().Object, s3.Object,
            EscenarioEntrax.Usuarios(EscenarioEntrax.UsuarioInactivo()).Object, jwt.Object,
            cifrado.Object, Mock.Of<ITokenRedemptionService>(),
            new SignupActivationService(db, patrocinio.Object, Mock.Of<IFastStartBonusService>(),
                Mock.Of<IRecurringBillingEnrollmentService>()),
            direcciones.Object, new PuertaDeFondosDePrueba(db),
            configuration: config);
        return new Montaje(handler, s3, patrocinio);
    }

    private static CompleteSignupRequest ConTarjeta() => new()
    {
        PaymentMethod = PaymentMethodType.CreditCard,
        CreditCard = new CreditCardInfoDto
        {
            Last4 = "4242", First6 = "411111", CardBrand = "Visa", ExpiryMonth = 12, ExpiryYear = 2030,
            Gateway = "spreedly", GatewayToken = "tok_simulado", CardToken = "pm_simulado"
        },
        // Con captura: con la tarjeta apagada tampoco se sube nada a S3.
        CheckoutScreenshotBase64      = Convert.ToBase64String([1, 2, 3]),
        CheckoutScreenshotContentType = "image/png"
    };

    public static TheoryData<string, IConfiguration?> Apagada => new()
    {
        { "sin configuración", null },
        { "sin la clave", TarjetaDelAlta.SinOpcion() },
        { "false", TarjetaDelAlta.Apagada() },
        { "vacía", TarjetaDelAlta.Con("") },
        { "ilegible", TarjetaDelAlta.Con("sí") }
    };

    [Theory]
    [MemberData(nameof(Apagada))]
    public async Task Con_la_tarjeta_apagada_el_alta_con_tarjeta_es_CARD_PAYMENTS_DISABLED_y_no_toca_nada(
        string caso, IConfiguration? config)
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);
        var m = Montar(db, config);

        var r = await m.Handler.Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, ConTarjeta()), default);

        r.IsSuccess.Should().BeFalse(caso);
        r.ErrorCode.Should().Be(TokenPurchaseErrors.CardPaymentsDisabled, caso);
        r.ErrorCode.Should().Be(CompleteSignupHandler.CardPaymentsDisabledCode);

        db.ChangeTracker.Clear();
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending, "el pedido no se reclama");
        (await db.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Pending);
        (await db.MembershipSubscriptions.SingleAsync()).SubscriptionStatus.Should().Be(MembershipStatus.Pending);
        (await db.CreditCards.CountAsync()).Should().Be(0, "la tarjeta no se guarda");
        (await db.MemberStatistics.CountAsync()).Should().Be(0);
        (await db.MemberStatisticDeltas.CountAsync()).Should().Be(0);
        m.S3.VerifyNoOtherCalls();
        m.Patrocinio.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Rechazada_la_tarjeta_el_aspirante_puede_pagar_con_otro_metodo()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);
        var m = Montar(db, TarjetaDelAlta.Apagada());

        (await m.Handler.Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, ConTarjeta()), default))
            .ErrorCode.Should().Be(TokenPurchaseErrors.CardPaymentsDisabled);

        var cripto = await m.Handler.Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId,
            new CompleteSignupRequest { PaymentMethod = PaymentMethodType.Crypto, CryptoCurrency = "BTC" }), default);

        cripto.IsSuccess.Should().BeTrue();
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Processing);
    }

    [Fact]
    public async Task Con_la_tarjeta_encendida_se_mantiene_el_comportamiento_actual()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);
        var m = Montar(db, TarjetaDelAlta.Encendida());

        var r = await m.Handler.Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, ConTarjeta()), default);

        r.IsSuccess.Should().BeTrue();
        r.Value!.AccessToken.Should().Be("access");
        db.ChangeTracker.Clear();
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Completed);
        (await db.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Active);
        (await db.CreditCards.SingleAsync()).Last4.Should().Be("4242");
    }

    [Fact]
    public async Task El_interruptor_solo_cierra_la_tarjeta()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);
        var m = Montar(db, TarjetaDelAlta.Apagada());

        var r = await m.Handler.Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId,
            new CompleteSignupRequest { PaymentMethod = PaymentMethodType.Crypto, CryptoCurrency = "BTC" }), default);

        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task El_controlador_responde_400_con_el_codigo()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(x => x.Send(It.IsAny<CompleteSignupCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<SignupResponse>.Failure(TokenPurchaseErrors.CardPaymentsDisabled, "x"));
        var controller = new SignupsController(mediator.Object, Mock.Of<IFraudFingerprintService>());

        var r = (await controller.CompleteSignup(EscenarioEntrax.OrderId, ConTarjeta(), default))
            .Should().BeAssignableTo<ObjectResult>().Subject;

        r.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        r.Value.Should().BeOfType<ApiResponse<SignupResponse>>()
         .Which.ErrorCode.Should().Be(TokenPurchaseErrors.CardPaymentsDisabled);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData("", false)]
    [InlineData("1", false)]
    [InlineData(null, false)]
    public void El_interruptor_solo_se_enciende_con_true(string? valor, bool encendida)
        => SignupCardPayments.IsEnabled(TarjetaDelAlta.Con(valor)).Should().Be(encendida);

    [Fact]
    public void Sin_configuracion_va_apagada()
        => SignupCardPayments.IsEnabled(null).Should().BeFalse();

    [Fact]
    public void El_appsettings_base_la_deja_apagada()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MLMConquerorGlobalEdition.slnx"))) dir = dir.Parent;
        dir.Should().NotBeNull();
        var c = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(dir!.FullName, "MLMConquerorGlobalEdition.SignupAPI", "appsettings.json"))
            .Build();

        c[SignupCardPayments.EnabledConfigKey].Should().BeOneOf(new[] { "False", "false" }, "tiene que estar escrita y apagada");
        SignupCardPayments.IsEnabled(c).Should().BeFalse();
    }
}
