using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

/// <summary>
/// Regla de Nick: nunca se cobra con el stub. SpreedlyCardGatewayService devuelve un "éxito" con un id
/// "simulated-…" y StubGatewayBase uno "STUB-…". Siendo RecurringBillingProcessor el único motor (Plan
/// K), daría la renovación por pagada con dinero inventado. No se renueva, no avanza el estado y el
/// pedido se cancela. El camino de comisiones (dinero real) no cambia.
/// </summary>
public class RenovacionSimuladaTests
{
    private static readonly DateTime Ahora = new(2026, 10, 20, 9, 0, 0);
    private static readonly DateTime InicioAnterior = Ahora.AddDays(-30);

    private static async Task<AppDbContext> SembrarAsync(bool comisionesPrimero = false, decimal comision = 0m)
    {
        var db = TestDbContextFactory.Create();
        db.RecurringBillingPlans.Add(new RecurringBillingPlan
        {
            Id = 1, Name = "Travel Advantage", CycleType = RecurringCycleType.Every30Days, RetryCadenceDays = "1,2",
            OnAllRetriesFail = RecurringFailurePolicy.RetryOnMonthlyAnniversary, StopAfterUnbilledDays = 90,
            PayFromCommissionBalanceFirst = comisionesPrimero, TokenTypeId = 100, IsActive = true, CreatedBy = "prueba"
        });
        db.MembershipLevels.Add(new MembershipLevel { Id = 3, Name = "Travel Advantage - Elite", SortOrder = 3, CreatedBy = "prueba" });
        db.Products.Add(new Product
        {
            Id = "prod-elite", Name = "Travel Advantage Elite", Description = "", ImageUrl = "", MembershipLevelId = 3,
            MonthlyFee = 119.97m, SetupFee = 0m, IsActive = true, CreatedBy = "prueba"
        });
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = "sub-1", MemberId = "AMB-000001", MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.Active,
            StartDate = InicioAnterior, CreatedBy = "prueba"
        });
        db.SubscriptionBillingStates.Add(new SubscriptionBillingState
        {
            Id = "estado-1", MembershipSubscriptionId = "sub-1", MemberId = "AMB-000001", RecurringBillingPlanId = 1,
            BillingAnchorDate = InicioAnterior, NextBillingDate = Ahora.Date, NextAttemptDate = Ahora.Date,
            Status = RecurringBillingStatus.Active, CreatedBy = "prueba"
        });
        if (comision > 0)
            db.CommissionEarnings.Add(new CommissionEarning
            {
                BeneficiaryMemberId = "AMB-000001", CommissionTypeId = 1, Amount = comision,
                Status = CommissionEarningStatus.Pending, EarnedDate = Ahora.AddDays(-8), PaymentDate = Ahora.AddDays(-1),
                CreatedBy = "prueba"
            });
        await db.SaveChangesAsync();
        return db;
    }

    private static RecurringBillingProcessor Procesador(AppDbContext db, string idDeTransaccion, string estado = "Success")
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(r => r.Now).Returns(Ahora);

        var plan = new GatewayRoutingPlan { RouteBucketKey = "prueba", Steps = new List<GatewayAttemptPlan>() };
        var ruta = new Mock<IGatewayRouter>();
        ruta.Setup(r => r.ResolveAsync(It.IsAny<GatewayRoutingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GatewayRoutingPlan>.Success(plan));

        var orquestador = new Mock<IGatewayChargeOrchestrator>();
        orquestador.Setup(o => o.ExecuteAsync(It.IsAny<GatewayRoutingPlan>(), It.IsAny<GatewayRoutingContext>(),
                It.IsAny<OrchestratorChargeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrchestratorChargeResult>.Success(new OrchestratorChargeResult
            {
                Status = estado, GatewayTransactionId = idDeTransaccion, PaymentHistoryId = "ph-1", AmountCharged = 119.97m
            }));

        return new RecurringBillingProcessor(
            db, new CommissionBalanceService(db, reloj.Object), new RecurringBillingScheduler(),
            ruta.Object, orquestador.Object, new Mock<ICardBrandDetector>().Object,
            new TransactionOnlyGate(db), reloj.Object, NullLogger<RecurringBillingProcessor>.Instance);
    }

    [Theory]
    [InlineData("simulated-7f3a")]
    [InlineData("STUB-NmiSpreedly-1")]
    public async Task Un_exito_simulado_no_renueva_ni_avanza_el_estado(string idSimulado)
    {
        await using var db = await SembrarAsync();

        var r = await Procesador(db, idSimulado).ProcessAsync("estado-1");

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(TokenPurchaseErrors.CardSimulatedCharge);
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Cancelled);
        (await db.RecurringBillingAttempts.CountAsync(a => a.Outcome == RecurringAttemptOutcome.Success)).Should().Be(0);

        var estado = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();
        estado.LastSuccessfulBillingDate.Should().BeNull();
        estado.NextAttemptDate.Should().Be(Ahora.Date);
        estado.CurrentAttemptIndex.Should().Be(0);
        (await db.MembershipSubscriptions.AsNoTracking().SingleAsync()).StartDate.Should().Be(InicioAnterior);
    }

    [Fact]
    public async Task Un_cobro_real_sigue_renovando()
    {
        await using var db = await SembrarAsync();

        var r = await Procesador(db, "ch_3Pq9real").ProcessAsync("estado-1");

        r.IsSuccess.Should().BeTrue();
        r.Value!.Outcome.Should().Be("Success");
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public async Task Con_comisiones_suficientes_paga_con_comisiones_y_ni_siquiera_llama_a_la_tarjeta()
    {
        await using var db = await SembrarAsync(comisionesPrimero: true, comision: 500m);

        var r = await Procesador(db, "simulated-no-se-llega").ProcessAsync("estado-1");

        r.IsSuccess.Should().BeTrue();
        r.Value!.FundingSource.Should().Be("CommissionBalance");
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Paid);
    }
}
