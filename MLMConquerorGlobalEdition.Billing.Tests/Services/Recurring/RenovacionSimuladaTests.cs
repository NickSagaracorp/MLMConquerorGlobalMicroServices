using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
/// Regla de Nick: nunca se cobra con el stub (Plan K, K19 y revisión I1).
/// <list type="bullet">
///   <item>Con <c>Billing:Recurring:CardEnabled</c> apagado (por defecto) el motor no llama al gateway:
///   intento Failed con CARD_DISABLED, cadencia normal y sin PaymentHistory.</item>
///   <item>Encendido, un "éxito" simulado (ids "simulated-…" o "STUB-…") es un fallo: intento Failed con
///   CARD_SIMULATED_CHARGE, cadencia normal, sin renovar.</item>
///   <item>El camino de comisiones (dinero real) no cambia.</item>
/// </list>
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
            StartDate = InicioAnterior, IsAutoRenew = true, CreatedBy = "prueba"
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

    private static IConfiguration Tarjeta(bool encendida) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [RecurringCardSwitch.ConfigKey] = encendida ? "true" : "false"
        }).Build();

    private static (RecurringBillingProcessor Procesador, Mock<IGatewayChargeOrchestrator> Orquestador) Montar(
        AppDbContext db, string idDeTransaccion, IConfiguration? configuracion)
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
                Status = "Success", GatewayTransactionId = idDeTransaccion, PaymentHistoryId = "ph-1", AmountCharged = 119.97m
            }));

        var p = new RecurringBillingProcessor(
            db, new CommissionBalanceService(db, reloj.Object), new RecurringBillingScheduler(),
            ruta.Object, orquestador.Object, new Mock<ICardBrandDetector>().Object,
            new TransactionOnlyGate(db), new Mock<MLMConquerorGlobalEdition.Billing.Services.AnnualFee.IAnnualFeeChargeService>().Object, reloj.Object, NullLogger<RecurringBillingProcessor>.Instance, configuracion);
        return (p, orquestador);
    }

    private static async Task ComprobarFalloConCadenciaAsync(AppDbContext db, string codigo)
    {
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Cancelled);
        var intento = await db.RecurringBillingAttempts.SingleAsync();
        intento.Outcome.Should().Be(RecurringAttemptOutcome.Failed);
        intento.FundingSource.Should().Be(RecurringFundingSource.CreditCard);
        intento.FailureReason.Should().StartWith(codigo);
        intento.PaymentHistoryId.Should().BeNull();

        var estado = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();
        estado.LastSuccessfulBillingDate.Should().BeNull();
        estado.CurrentAttemptIndex.Should().Be(1);
        estado.NextAttemptDate.Should().Be(Ahora.Date.AddDays(1)); // cadencia "1,2"
        estado.LastFailureReason.Should().StartWith(codigo);
        (await db.MembershipSubscriptions.AsNoTracking().SingleAsync()).StartDate.Should().Be(InicioAnterior);
    }

    [Fact]
    public async Task Con_la_tarjeta_apagada_por_defecto_no_llama_al_gateway_y_sigue_la_cadencia()
    {
        await using var db = await SembrarAsync();
        var (procesador, orquestador) = Montar(db, "ch_real", configuracion: null);

        var r = await procesador.ProcessAsync("estado-1");

        r.IsSuccess.Should().BeTrue();
        r.Value!.Outcome.Should().Be("Failed");
        r.Value.FailureReason.Should().StartWith(RecurringCardSwitch.CardDisabledCode);
        orquestador.VerifyNoOtherCalls();
        (await db.PaymentHistories.CountAsync()).Should().Be(0);
        await ComprobarFalloConCadenciaAsync(db, RecurringCardSwitch.CardDisabledCode);
    }

    [Fact]
    public async Task Con_la_tarjeta_apagada_explicitamente_tampoco()
    {
        await using var db = await SembrarAsync();
        var (procesador, orquestador) = Montar(db, "ch_real", Tarjeta(false));

        (await procesador.ProcessAsync("estado-1")).Value!.FailureReason.Should().StartWith(RecurringCardSwitch.CardDisabledCode);
        orquestador.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("simulated-7f3a")]
    [InlineData("STUB-NmiSpreedly-1")]
    public async Task Encendida_un_exito_simulado_es_un_fallo_con_cadencia(string idSimulado)
    {
        await using var db = await SembrarAsync();
        var (procesador, _) = Montar(db, idSimulado, Tarjeta(true));

        var r = await procesador.ProcessAsync("estado-1");

        r.IsSuccess.Should().BeTrue();
        r.Value!.Outcome.Should().Be("Failed");
        r.Value.FailureReason.Should().StartWith(TokenPurchaseErrors.CardSimulatedCharge);
        await ComprobarFalloConCadenciaAsync(db, TokenPurchaseErrors.CardSimulatedCharge);
    }

    [Fact]
    public async Task Encendida_un_cobro_real_sigue_renovando()
    {
        await using var db = await SembrarAsync();
        var (procesador, _) = Montar(db, "ch_3Pq9real", Tarjeta(true));

        var r = await procesador.ProcessAsync("estado-1");

        r.IsSuccess.Should().BeTrue();
        r.Value!.Outcome.Should().Be("Success");
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public async Task Con_comisiones_suficientes_paga_con_comisiones_y_ni_siquiera_mira_la_tarjeta()
    {
        await using var db = await SembrarAsync(comisionesPrimero: true, comision: 500m);
        var (procesador, orquestador) = Montar(db, "simulated-no-se-llega", configuracion: null);

        var r = await procesador.ProcessAsync("estado-1");

        r.IsSuccess.Should().BeTrue();
        r.Value!.FundingSource.Should().Be("CommissionBalance");
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Paid);
        orquestador.VerifyNoOtherCalls();
    }
}
