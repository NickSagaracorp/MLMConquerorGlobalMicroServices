using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MediatR;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Crypto;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Controllers;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.ConfirmCryptoPayment;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Signups;

/// <summary>
/// Una compra de tokens en cripto también tiene CryptoPaymentConfirmation. SignupAPI no la trata
/// como un alta: no hay suscripción que activar. La confirma Billing. Y el alta muestra la dirección
/// de depósito configurada en vez de prometer un correo.
/// </summary>
public class CriptoDeTokensNoEsUnAltaTests
{
    [Fact]
    public async Task La_confirmacion_de_altas_no_encuentra_una_compra_de_tokens()
    {
        await using var db = InMemoryDbHelper.Create();
        db.Orders.Add(new Orders { Id = "ord-t", MemberId = "AMB-1", Status = OrderStatus.Processing, OrderDate = DateTime.Now, CreatedBy = "seed" });
        db.CryptoPaymentConfirmations.Add(new CryptoPaymentConfirmation
        {
            OrderId = "ord-t", MemberId = "AMB-1", MemberEmail = "a@x.com", CryptoCurrency = "BTC",
            AmountDue = 99.99m, Purpose = CryptoPaymentPurpose.TokenPurchase, CreatedBy = "seed", CreationDate = DateTime.Now
        });
        await db.SaveChangesAsync();

        var usuarios = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        var handler = new ConfirmCryptoPaymentHandler(
            db, Mock.Of<IDateTimeProvider>(), usuarios.Object, new Mock<ISignupActivationService>(MockBehavior.Strict).Object,
            Mock.Of<ILogger<ConfirmCryptoPaymentHandler>>());

        var r = await handler.Handle(new ConfirmCryptoPaymentCommand(
            "ord-t", new ConfirmCryptoPaymentRequest { CryptoTransactionId = "0x1" }, "admin-1", "admin@x.com"), default);

        r.ErrorCode.Should().Be("CRYPTO_PAYMENT_NOT_FOUND");
        db.CryptoPaymentConfirmations.Single().Status.Should().Be(CryptoPaymentConfirmationStatus.AwaitingPayment);
    }

    [Fact]
    public async Task El_alta_puede_pedir_las_direcciones_configuradas()
    {
        var lector = new Mock<ICryptoDepositAddressReader>();
        lector.Setup(l => l.GetConfiguredAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(new[] { new CryptoDepositAddressDto { Currency = "BTC", Address = "bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh" } });
        var controller = new SignupsController(Mock.Of<IMediator>(), Mock.Of<IFraudFingerprintService>());

        var r = await controller.GetCryptoDepositAddresses(lector.Object, default);

        ((OkObjectResult)r).Value.Should().BeOfType<ApiResponse<IReadOnlyList<CryptoDepositAddressDto>>>()
            .Which.Data!.Single().Currency.Should().Be("BTC");
    }
}
