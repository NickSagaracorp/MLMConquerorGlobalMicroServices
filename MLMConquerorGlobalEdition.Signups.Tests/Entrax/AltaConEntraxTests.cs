using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediatR;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Crypto;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Controllers;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.CompleteSignup;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// Completar el alta con rublos por SBP: solo donde está encendido y para el país del aspirante; deja
/// todo pendiente como la cripto (sin comisiones, sin sesión) y devuelve el QR.
/// </summary>
public class AltaConEntraxTests
{
    private static readonly EntraxSignupPaymentDto Qr = new()
    {
        PaymentRef = "ref-1", QrLink = "https://qr.nspk.ru/AS100", AmountUsd = 80m, AmountRub = 7640m, SecondsRemaining = 900
    };

    private static CompleteSignupRequest Pide() => new() { PaymentMethod = PaymentMethodType.Entrax };

    /// <summary>Un checkout que guarda como el de verdad (StartAsync guarda el contexto) y devuelve el QR.</summary>
    private static Mock<IEntraxSignupCheckout> Checkout(AppDbContext db, Result<EntraxSignupPaymentDto>? resultado = null)
    {
        var m = new Mock<IEntraxSignupCheckout>();
        m.Setup(c => c.IsOfferedFor(It.IsAny<string?>())).Returns<string?>(p => p == "RU");
        m.Setup(c => c.StartAsync(It.IsAny<Orders>(), It.IsAny<MemberProfile>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
         .Returns(async (Orders _, MemberProfile _, DateTime _, CancellationToken _) =>
         {
             await db.SaveChangesAsync();
             return resultado ?? Result<EntraxSignupPaymentDto>.Success(Qr);
         });
        return m;
    }

    private static CompleteSignupHandler Handler(
        AppDbContext db, Mock<UserManager<ApplicationUser>> usuarios, IEntraxSignupCheckout? checkout,
        ISponsorBonusService? sponsor = null)
        => new(db, EscenarioEntrax.Reloj().Object, Mock.Of<IS3FileService>(), usuarios.Object, Mock.Of<IJwtService>(),
               Mock.Of<IEncryptionService>(), Mock.Of<ITokenRedemptionService>(),
               new SignupActivationService(db, sponsor ?? Mock.Of<ISponsorBonusService>(), Mock.Of<IFastStartBonusService>(),
                   Mock.Of<IRecurringBillingEnrollmentService>()),
               Mock.Of<ICryptoDepositAddressReader>(), new PuertaDeFondosDePrueba(db),
               entraxCheckout: checkout);

    [Fact]
    public async Task Fuera_de_los_paises_configurados_es_ENTRAX_NOT_AVAILABLE()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, pais: "US");
        var checkout = Checkout(db);

        var r = await Handler(db, EscenarioEntrax.Usuarios(EscenarioEntrax.UsuarioInactivo()), checkout.Object)
            .Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, Pide()), default);

        r.ErrorCode.Should().Be(EntraxSignupErrors.NotAvailable);
        checkout.Verify(c => c.StartAsync(It.IsAny<Orders>(), It.IsAny<MemberProfile>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task Sin_Entrax_registrado_tampoco_esta_disponible()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);

        var r = await Handler(db, EscenarioEntrax.Usuarios(EscenarioEntrax.UsuarioInactivo()), checkout: null)
            .Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, Pide()), default);

        r.ErrorCode.Should().Be(EntraxSignupErrors.NotAvailable);
    }

    [Fact]
    public async Task En_Rusia_devuelve_el_QR_y_deja_todo_pendiente_sin_sesion_ni_comisiones()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);
        var usuario = EscenarioEntrax.UsuarioInactivo();
        var usuarios = EscenarioEntrax.Usuarios(usuario);
        var sponsor = new Mock<ISponsorBonusService>();
        var checkout = Checkout(db);

        var r = await Handler(db, usuarios, checkout.Object, sponsor.Object)
            .Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, Pide()), default);

        r.IsSuccess.Should().BeTrue();
        r.Value!.Entrax.Should().BeSameAs(Qr);
        r.Value.AccessToken.Should().BeNull("al pagar, la persona inicia sesión; nadie recibe una sesión por una URL anónima.");
        usuario.IsActive.Should().BeFalse();
        usuarios.Verify(u => u.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        (await db.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Pending);
        (await db.MembershipSubscriptions.SingleAsync()).SubscriptionStatus.Should().Be(MembershipStatus.Pending);
        sponsor.Verify(s => s.ComputeAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        checkout.Verify(c => c.StartAsync(
            It.Is<Orders>(o => o.Id == EscenarioEntrax.OrderId), It.Is<MemberProfile>(m => m.MemberId == EscenarioEntrax.MemberId),
            EscenarioEntrax.Ahora, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Un_segundo_intento_no_duplica_las_estadisticas_del_miembro()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);
        db.MemberStatistics.Add(new MemberStatisticEntity
        {
            MemberId = EscenarioEntrax.MemberId, PersonalPoints = 10, EnrollmentPoints = 10,
            CreatedBy = "primer-intento", CreationDate = EscenarioEntrax.Ahora
        });
        await db.SaveChangesAsync();

        await Handler(db, EscenarioEntrax.Usuarios(EscenarioEntrax.UsuarioInactivo()), Checkout(db).Object)
            .Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, Pide()), default);

        (await db.MemberStatistics.CountAsync(s => s.MemberId == EscenarioEntrax.MemberId)).Should().Be(1);
    }

    [Fact]
    public async Task El_primer_intento_si_crea_las_estadisticas()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);

        await Handler(db, EscenarioEntrax.Usuarios(EscenarioEntrax.UsuarioInactivo()), Checkout(db).Object)
            .Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, Pide()), default);

        (await db.MemberStatistics.CountAsync(s => s.MemberId == EscenarioEntrax.MemberId)).Should().Be(1);
    }

    [Fact]
    public async Task El_error_del_QR_llega_tal_cual()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);
        var checkout = Checkout(db, Result<EntraxSignupPaymentDto>.Failure(EntraxSignupErrors.Unavailable, "x"));

        var r = await Handler(db, EscenarioEntrax.Usuarios(EscenarioEntrax.UsuarioInactivo()), checkout.Object)
            .Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, Pide()), default);

        r.ErrorCode.Should().Be(EntraxSignupErrors.Unavailable);
    }

    [Theory]
    [InlineData(EntraxSignupErrors.Unavailable, StatusCodes.Status502BadGateway)]
    [InlineData(EntraxSignupErrors.InProgress, StatusCodes.Status409Conflict)]
    [InlineData(EntraxSignupErrors.NotAvailable, StatusCodes.Status400BadRequest)]
    [InlineData(CompleteSignupHandler.SignupAlreadySettledCode, StatusCodes.Status409Conflict)]
    public async Task El_controlador_traduce_los_errores_de_Entrax(string codigo, int http)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CompleteSignupCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<SignupResponse>.Failure(codigo, "x"));
        var controller = new SignupsController(mediator.Object, Mock.Of<IFraudFingerprintService>());

        var r = await controller.CompleteSignup(EscenarioEntrax.OrderId, Pide(), default);

        r.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(http);
    }
}
