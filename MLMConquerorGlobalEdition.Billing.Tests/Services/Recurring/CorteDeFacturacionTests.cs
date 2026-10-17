using FluentAssertions;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.CardGateway;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

/// <summary>
/// Cuando un estado de facturación pasa el tope de días sin facturar, se para Y la membresía pasa a
/// su estado terminal (HoldByBilling o Expired, según la política del plan). Antes, ese cambio se
/// hacía después del último SaveChanges y el método retornaba: solo se guardaba si el mismo
/// contexto volvía a guardar después (otro estado de la misma pasada). Con una llamada suelta, o con
/// el último estado de la pasada, la membresía seguía Active.
/// </summary>
public class CorteDeFacturacionTests
{
    private const string SubscriptionId = "sub-corte";

    private static async Task SembrarAsync(string dbName, RecurringFailurePolicy politica)
    {
        await using var db = TestDbContextFactory.Create(dbName);
        db.RecurringBillingPlans.Add(new RecurringBillingPlan
        {
            Id = 1, Name = "Plan", CycleType = RecurringCycleType.Every30Days, RetryCadenceDays = "0,3",
            OnAllRetriesFail = politica, StopAfterUnbilledDays = 90, CreatedBy = "prueba"
        });
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = SubscriptionId, MemberId = "AMB-000001", MembershipLevelId = 2,
            SubscriptionStatus = MembershipStatus.Active, StartDate = new DateTime(2026, 1, 1), CreatedBy = "prueba"
        });
        db.SubscriptionBillingStates.Add(new SubscriptionBillingState
        {
            Id = "estado-corte", MembershipSubscriptionId = SubscriptionId, MemberId = "AMB-000001",
            RecurringBillingPlanId = 1, BillingAnchorDate = new DateTime(2026, 1, 1),
            LastSuccessfulBillingDate = new DateTime(2026, 6, 1), NextAttemptDate = new DateTime(2026, 10, 1),
            Status = RecurringBillingStatus.Retrying, CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();
    }

    private static RecurringBillingProcessor Procesador(Repository.Context.AppDbContext db)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(r => r.Now).Returns(new DateTime(2026, 10, 16, 9, 0, 0));
        return new RecurringBillingProcessor(
            db,
            new Mock<ICommissionBalanceService>().Object,
            new Mock<IRecurringBillingScheduler>().Object,
            new Mock<IGatewayRouter>().Object,
            new Mock<IGatewayChargeOrchestrator>().Object,
            new Mock<ICardBrandDetector>().Object,
            reloj.Object,
            NullLogger<RecurringBillingProcessor>.Instance);
    }

    [Theory]
    [InlineData(RecurringFailurePolicy.RetryOnMonthlyAnniversary, MembershipStatus.HoldByBilling)]
    [InlineData(RecurringFailurePolicy.MarkExpired,               MembershipStatus.Expired)]
    public async Task Pasado_el_tope_de_dias_la_membresia_queda_en_su_estado_terminal_guardado(
        RecurringFailurePolicy politica, MembershipStatus esperado)
    {
        var dbName = Guid.NewGuid().ToString();
        await SembrarAsync(dbName, politica);

        await using (var db = TestDbContextFactory.Create(dbName))
        {
            var r = await Procesador(db).ProcessAsync("estado-corte");
            r.IsSuccess.Should().BeTrue();
            r.Value!.Outcome.Should().Be("Skipped");
        }

        await using var lectura = TestDbContextFactory.Create(dbName);
        (await lectura.SubscriptionBillingStates.SingleAsync()).Status.Should().Be(RecurringBillingStatus.Stopped);
        (await lectura.MembershipSubscriptions.SingleAsync()).SubscriptionStatus.Should().Be(esperado);
    }
}
