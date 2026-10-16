using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Services.Crypto;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Controllers;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Membership.Commands.UpgradeMembership;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.CompleteSignup;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Signups;

/// <summary>
/// CompleteSignup admite una lista cerrada de métodos (Token, Crypto, Entrax, y CreditCard solo con
/// Signup:CardEnabled). Cualquier otro valor —también uno que no está en el enum— es
/// <c>400 PAYMENT_METHOD_NOT_SUPPORTED</c> antes de cualquier efecto: un método nuevo no puede activar
/// por caer en la rama de "activar" sin que nadie lo haya decidido.
/// </summary>
public class MetodosAdmitidosEnElAltaTests
{
    [Theory]
    [InlineData(99)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Un_metodo_que_no_esta_en_la_lista_es_PAYMENT_METHOD_NOT_SUPPORTED_y_no_toca_nada(int metodo)
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);
        var s3 = new Mock<IS3FileService>();
        var patrocinio = new Mock<ISponsorBonusService>();
        var handler = new CompleteSignupHandler(
            db, EscenarioEntrax.Reloj().Object, s3.Object,
            EscenarioEntrax.Usuarios(EscenarioEntrax.UsuarioInactivo()).Object, Mock.Of<IJwtService>(),
            Mock.Of<IEncryptionService>(), Mock.Of<ITokenRedemptionService>(),
            new SignupActivationService(db, patrocinio.Object, Mock.Of<IFastStartBonusService>(),
                Mock.Of<IRecurringBillingEnrollmentService>()),
            Mock.Of<ICryptoDepositAddressReader>(), new PuertaDeFondosDePrueba(db),
            configuration: TarjetaDelAlta.Encendida());

        var r = await handler.Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, new CompleteSignupRequest
        {
            PaymentMethod                 = (PaymentMethodType)metodo,
            CheckoutScreenshotBase64      = Convert.ToBase64String([1, 2, 3]),
            CheckoutScreenshotContentType = "image/png"
        }), default);

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be("PAYMENT_METHOD_NOT_SUPPORTED");
        r.ErrorCode.Should().Be(CompleteSignupHandler.PaymentMethodNotSupportedCode);
        db.ChangeTracker.Clear();
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
        (await db.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Pending);
        (await db.MemberStatistics.CountAsync()).Should().Be(0);
        s3.VerifyNoOtherCalls();
        patrocinio.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task El_controlador_responde_400_con_el_codigo()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(x => x.Send(It.IsAny<CompleteSignupCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<SignupResponse>.Failure(CompleteSignupHandler.PaymentMethodNotSupportedCode, "x"));
        var controller = new SignupsController(mediator.Object, Mock.Of<IFraudFingerprintService>());

        var r = (await controller.CompleteSignup(EscenarioEntrax.OrderId,
                new CompleteSignupRequest { PaymentMethod = (PaymentMethodType)99 }, default))
            .Should().BeAssignableTo<ObjectResult>().Subject;

        r.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        r.Value.Should().BeOfType<ApiResponse<SignupResponse>>()
         .Which.ErrorCode.Should().Be(TokenPurchaseErrors.PaymentMethodNotSupported);
    }

    // ── Los interruptores desde el entorno y su inyección ───────────────────

    [Fact]
    public void Las_variables_de_entorno_encienden_los_interruptores()
    {
        const string tarjeta = "Signup__CardEnabled";
        const string subida  = "Membership__UpgradeEnabled";
        var antes = (Environment.GetEnvironmentVariable(tarjeta), Environment.GetEnvironmentVariable(subida));
        try
        {
            Environment.SetEnvironmentVariable(tarjeta, "true");
            Environment.SetEnvironmentVariable(subida, "true");
            var c = new ConfigurationBuilder().AddEnvironmentVariables().Build();

            SignupCardPayments.IsEnabled(c).Should().BeTrue();
            MembershipUpgrades.IsEnabled(c).Should().BeTrue();

            Environment.SetEnvironmentVariable(tarjeta, "false");
            Environment.SetEnvironmentVariable(subida, null);
            c = new ConfigurationBuilder().AddEnvironmentVariables().Build();

            SignupCardPayments.IsEnabled(c).Should().BeFalse();
            MembershipUpgrades.IsEnabled(c).Should().BeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable(tarjeta, antes.Item1);
            Environment.SetEnvironmentVariable(subida, antes.Item2);
        }
    }

    /// <summary>
    /// El contenedor solo inyecta IConfiguration si el constructor la pide: sin ella los interruptores
    /// quedarían apagados para siempre en producción (falla cerrada, pero no se podrían encender).
    /// Cada tipo tiene un único constructor público, el que usa el contenedor.
    /// </summary>
    [Theory]
    [InlineData(typeof(CompleteSignupHandler))]
    [InlineData(typeof(UpgradeMembershipHandler))]
    [InlineData(typeof(EntraxSignupStatusService))]
    public void El_contenedor_les_inyecta_la_configuracion(Type tipo)
    {
        var constructores = tipo.GetConstructors();
        constructores.Should().ContainSingle();
        constructores[0].GetParameters().Should().Contain(p => p.ParameterType == typeof(IConfiguration));
    }
}
