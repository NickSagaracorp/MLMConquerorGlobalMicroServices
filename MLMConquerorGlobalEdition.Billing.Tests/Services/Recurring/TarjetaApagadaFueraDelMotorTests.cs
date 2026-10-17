using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.DTOs;
using MLMConquerorGlobalEdition.Billing.Features.RenewMembership;
using MLMConquerorGlobalEdition.Billing.Jobs;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.CardGateway;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

/// <summary>
/// Plan K (revisión I2 y C3): la tarjeta tampoco se cobra fuera del motor mientras sea un stub.
/// <c>POST api/v1/billing/memberships/renew</c> responde CARD_DISABLED sin pedido ni gateway, y el cobro
/// diferido nunca da por bueno un éxito simulado.
/// </summary>
public class TarjetaApagadaFueraDelMotorTests
{
    private static readonly DateTime Ahora = new(2026, 10, 20, 9, 0, 0);

    private static IDateTimeProvider Reloj()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(r => r.Now).Returns(Ahora);
        return m.Object;
    }

    [Fact]
    public async Task La_renovacion_manual_con_tarjeta_responde_CARD_DISABLED_sin_pedido_ni_gateway()
    {
        await using var db = TestDbContextFactory.Create();
        db.MemberProfiles.Add(new MemberProfile
        {
            UserId = Guid.NewGuid(), MemberId = "AMB-1", Email = "a@b.c", FirstName = "A", LastName = "B", Country = "US",
            MemberType = MemberType.Ambassador, Status = MemberAccountStatus.Active, CreatedBy = "prueba"
        });
        db.MembershipLevels.Add(new MembershipLevel { Id = 3, Name = "Elite", SortOrder = 3, RenewalPrice = 119.97m, CreatedBy = "prueba" });
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = "sub-1", MemberId = "AMB-1", MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.Active,
            StartDate = Ahora.AddDays(-30), CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();
        var ruta = new Mock<IGatewayRouter>();
        var orquestador = new Mock<IGatewayChargeOrchestrator>();

        var r = await new RenewMembershipHandler(db, Mock.Of<IGatewayResolver>(), ruta.Object, orquestador.Object,
                Mock.Of<ICardBrandDetector>(), Mock.Of<ICurrentUserService>(u => u.UserId == "AMB-1"), Reloj())
            .Handle(new RenewMembershipCommand(new MembershipRenewalRequest { MemberId = "AMB-1" }), CancellationToken.None);

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(RecurringCardSwitch.CardDisabledCode);
        (await db.Orders.CountAsync()).Should().Be(0);
        ruta.VerifyNoOtherCalls();
        orquestador.VerifyNoOtherCalls();
        (await db.MembershipSubscriptions.AsNoTracking().SingleAsync()).StartDate.Should().Be(Ahora.AddDays(-30));
    }

    [Theory]
    [InlineData("simulated-1")]
    [InlineData("STUB-NmiSpreedly-2")]
    public async Task El_cobro_diferido_no_da_por_bueno_un_exito_simulado(string idSimulado)
    {
        await using var db = TestDbContextFactory.Create();
        var gateway = new Mock<ICardGatewayService>();
        gateway.Setup(g => g.ChargeAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(Result<GatewayChargeResult>.Success(new GatewayChargeResult { GatewayTransactionId = idSimulado, Status = "Success" }));
        var resolver = new Mock<ICardGatewayResolver>();
        resolver.Setup(r => r.Resolve(It.IsAny<CardProcessor>())).Returns(gateway.Object);

        await new DelayedFallbackChargeJob(db, resolver.Object, Reloj(), NullLogger<DelayedFallbackChargeJob>.Instance)
            .ExecuteAsync("bucket", "AMB-1", (int)CardProcessor.NmiSpreedly, 119.97m, "USD", 1, "ord-1", "tok", null, "renovacion");

        (await db.PaymentHistories.CountAsync()).Should().Be(0);
        var intento = await db.GatewayChargeAttempts.SingleAsync();
        intento.Outcome.Should().Be("Failed");
        intento.FailureReason.Should().StartWith(TokenPurchaseErrors.CardSimulatedCharge);
        intento.PaymentHistoryId.Should().BeNull();
    }
}
