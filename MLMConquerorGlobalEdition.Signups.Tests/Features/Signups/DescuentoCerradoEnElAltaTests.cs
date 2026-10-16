using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Crypto;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Controllers;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.CompleteSignup;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Signups;

/// <summary>
/// No existe ningún sistema de códigos de descuento: nadie validaba el código y cualquier cadena de 4 a 32
/// caracteres activaba la membresía y generaba comisiones gratis en un endpoint anónimo. Completar el alta
/// con <c>DiscountCode</c> es siempre <c>400 DISCOUNT_CODES_DISABLED</c> y no toca nada; no hay interruptor
/// porque no hay nada que encender. Tampoco con la tarjeta encendida.
/// </summary>
public class DescuentoCerradoEnElAltaTests
{
    [Theory]
    [InlineData("FREE100")]
    [InlineData("TI-LOADER")]
    [InlineData(null)]
    public async Task Completar_con_un_codigo_de_descuento_es_DISCOUNT_CODES_DISABLED_y_no_toca_nada(string? codigo)
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);
        var s3 = new Mock<IS3FileService>();
        var patrocinio = new Mock<ISponsorBonusService>();
        var usuario = EscenarioEntrax.UsuarioInactivo();
        var usuarios = EscenarioEntrax.Usuarios(usuario);
        var handler = new CompleteSignupHandler(
            db, EscenarioEntrax.Reloj().Object, s3.Object, usuarios.Object, Mock.Of<IJwtService>(),
            Mock.Of<IEncryptionService>(), Mock.Of<ITokenRedemptionService>(),
            new SignupActivationService(db, patrocinio.Object, Mock.Of<IFastStartBonusService>(),
                Mock.Of<IRecurringBillingEnrollmentService>()),
            Mock.Of<ICryptoDepositAddressReader>(), new PuertaDeFondosDePrueba(db),
            configuration: TarjetaDelAlta.Encendida());

        var r = await handler.Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, new CompleteSignupRequest
        {
            PaymentMethod                 = PaymentMethodType.DiscountCode,
            DiscountCode                  = codigo,
            CheckoutScreenshotBase64      = Convert.ToBase64String([1, 2, 3]),
            CheckoutScreenshotContentType = "image/png"
        }), default);

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be("DISCOUNT_CODES_DISABLED");
        r.ErrorCode.Should().Be(CompleteSignupHandler.DiscountCodesDisabledCode);

        db.ChangeTracker.Clear();
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
        (await db.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Pending);
        (await db.MembershipSubscriptions.SingleAsync()).SubscriptionStatus.Should().Be(MembershipStatus.Pending);
        (await db.MemberStatistics.CountAsync()).Should().Be(0);
        (await db.MemberStatisticDeltas.CountAsync()).Should().Be(0);
        usuario.IsActive.Should().BeFalse();
        usuarios.Verify(u => u.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        s3.VerifyNoOtherCalls();
        patrocinio.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task El_controlador_responde_400_con_el_codigo()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(x => x.Send(It.IsAny<CompleteSignupCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<SignupResponse>.Failure(CompleteSignupHandler.DiscountCodesDisabledCode, "x"));
        var controller = new SignupsController(mediator.Object, Mock.Of<IFraudFingerprintService>());

        var r = (await controller.CompleteSignup(EscenarioEntrax.OrderId,
                new CompleteSignupRequest { PaymentMethod = PaymentMethodType.DiscountCode, DiscountCode = "FREE100" }, default))
            .Should().BeAssignableTo<ObjectResult>().Subject;

        r.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        r.Value.Should().BeOfType<ApiResponse<SignupResponse>>()
         .Which.ErrorCode.Should().Be("DISCOUNT_CODES_DISABLED");
    }

    [Theory]
    [InlineData("Signup.razor")]
    [InlineData("MemberJoin.razor")]
    public void Ninguna_pagina_del_alta_paga_con_codigo_de_descuento(string pagina)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MLMConquerorGlobalEdition.slnx"))) dir = dir.Parent;
        var texto = File.ReadAllText(Path.Combine(dir!.FullName, "MLMConquerorGlobalEdition.Signups.Client", "Pages", pagina));

        texto.Should().NotContain("\"DiscountCode\"").And.NotContain("PaymentMethod = 4");
    }
}
