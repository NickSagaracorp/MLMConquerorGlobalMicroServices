using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Jobs;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Seeders;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

/// <summary>
/// Plan K (K1, K2). La cuota anual es por miembro: el plan anual nunca crea estados a partir de una
/// suscripción (si no, una suscripción de nivel 1 la cobraría como membresía y otra vez como cuota), y
/// un estado de cuota anual, que no tiene suscripción, no emite puntos ni disparadores de comisión.
/// </summary>
public class PlanAnualSinEstadosPorSuscripcionTests
{
    private static readonly DateTime Ahora = new(2027, 6, 10, 8, 0, 0);

    private static IDateTimeProvider Reloj()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(Ahora);
        return m.Object;
    }

    private static async Task<AppDbContext> ConPlanesAsync()
    {
        var db = TestDbContextFactory.Create();
        db.MembershipLevels.AddRange(
            new MembershipLevel { Id = 1, Name = "Lifestyle Ambassador", SortOrder = 1, CreatedBy = "prueba" },
            new MembershipLevel { Id = 3, Name = "Travel Advantage - Elite", SortOrder = 3, CreatedBy = "prueba" });
        db.Products.AddRange(
            new Product { Id = "prod-amb", Name = "Lifestyle Ambassador", Description = "", ImageUrl = "", MembershipLevelId = 1, MonthlyFee = 0m, SetupFee = 99m, AnnualPrice = 99m, IsActive = true, CreatedBy = "prueba" },
            new Product { Id = "prod-elite", Name = "Travel Advantage Elite", Description = "", ImageUrl = "", MembershipLevelId = 3, MonthlyFee = 119.97m, SetupFee = 0m, IsActive = true, CreatedBy = "prueba" });

        var anual = new RecurringBillingPlan
        {
            Id = 2, Name = "Lifestyle Ambassador (Annual)", CycleType = RecurringCycleType.AnnualFromLastBilling,
            RetryCadenceDays = "1,1,1,2,2,5,5", OnAllRetriesFail = RecurringFailurePolicy.MarkExpired,
            PayFromCommissionBalanceFirst = true, IsActive = true, IsAnnualFeePlan = true, CreatedBy = "prueba"
        };
        anual.PlanProducts.Add(new RecurringBillingPlanProduct { ProductId = "prod-amb", CreatedBy = "prueba" });
        var viajes = new RecurringBillingPlan
        {
            Id = 1, Name = "Travel Advantage", CycleType = RecurringCycleType.Every30Days, RetryCadenceDays = "1,2",
            OnAllRetriesFail = RecurringFailurePolicy.RetryOnMonthlyAnniversary, IsActive = true, CreatedBy = "prueba"
        };
        viajes.PlanProducts.Add(new RecurringBillingPlanProduct { ProductId = "prod-elite", CreatedBy = "prueba" });
        db.RecurringBillingPlans.AddRange(anual, viajes);
        await db.SaveChangesAsync();
        return db;
    }

    private static MembershipSubscription Suscripcion(string id, int nivel) => new()
    {
        Id = id, MemberId = "AMB-" + id, MembershipLevelId = nivel, SubscriptionStatus = MembershipStatus.Active,
        StartDate = Ahora.AddDays(-10), CreatedBy = "prueba"
    };

    [Fact]
    public async Task Una_suscripcion_de_nivel_1_no_crea_un_estado_en_el_plan_anual()
    {
        await using var db = await ConPlanesAsync();
        var sub = Suscripcion("s1", 1);
        db.MembershipSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        await new RecurringBillingEnrollmentService(db, Reloj()).EnsureStateForSubscriptionAsync(sub, "prueba");

        (await db.SubscriptionBillingStates.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Una_suscripcion_de_Travel_Advantage_si_crea_su_estado()
    {
        await using var db = await ConPlanesAsync();
        var sub = Suscripcion("s3", 3);
        db.MembershipSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        await new RecurringBillingEnrollmentService(db, Reloj()).EnsureStateForSubscriptionAsync(sub, "prueba");

        var estado = await db.SubscriptionBillingStates.SingleAsync();
        estado.RecurringBillingPlanId.Should().Be(1);
        estado.Purpose.Should().Be(BillingStatePurpose.Membership);
        estado.MembershipSubscriptionId.Should().Be("s3");
    }

    [Fact]
    public async Task El_barrido_tampoco_crea_estados_por_suscripcion_en_el_plan_anual()
    {
        await using var db = await ConPlanesAsync();
        db.MembershipSubscriptions.Add(Suscripcion("s1", 1));
        await db.SaveChangesAsync();

        var barrido = new RecurringBillingSweepJob(db, new Mock<IRecurringBillingProcessor>().Object,
            new RecurringBillingEnrollmentService(db, Reloj()), Reloj(), NullLogger<RecurringBillingSweepJob>.Instance);
        await barrido.ExecuteAsync();

        (await db.SubscriptionBillingStates.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("Success", RecurringBillingStatus.Active)]
    [InlineData("Failed", RecurringBillingStatus.Stopped)]
    public async Task Un_estado_de_cuota_anual_no_emite_puntos_ni_disparadores(string resultado, RecurringBillingStatus estadoFinal)
    {
        await using var db = TestDbContextFactory.Create();
        db.RecurringBillingBatches.Add(new RecurringBillingBatch
        {
            Id = "lote", RunDate = Ahora.Date, Gateway = CardProcessor.NmiSpreedly, WorkerCount = 1,
            ScheduledStartTime = Ahora, CaseCount = 1, Status = RecurringBillingBatchStatus.Planned,
            CreatedBy = "prueba", CreationDate = Ahora, LastUpdateDate = Ahora
        });
        db.RecurringBillingBatchShards.Add(new RecurringBillingBatchShard
        {
            Id = "trozo", BatchId = "lote", ShardIndex = 0, IdRangeStart = 1, IdRangeEnd = 100,
            Status = RecurringBillingBatchStatus.Planned, CreatedBy = "prueba", CreationDate = Ahora, LastUpdateDate = Ahora
        });
        db.SubscriptionBillingStates.Add(new SubscriptionBillingState
        {
            Id = "anual-1", Purpose = BillingStatePurpose.AnnualFee, MembershipSubscriptionId = null, MemberId = "AMB-1",
            RecurringBillingPlanId = 2, BillingAnchorDate = Ahora.AddYears(-1), NextBillingDate = Ahora.Date,
            NextAttemptDate = Ahora.Date, Status = estadoFinal, ShardKey = 5, CreatedBy = "prueba", CreationDate = Ahora
        });
        await db.SaveChangesAsync();

        var procesador = new Mock<IRecurringBillingProcessor>();
        procesador.Setup(p => p.ProcessAsync("anual-1", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RecurringBillingProcessorResult>.Success(new RecurringBillingProcessorResult
            {
                BillingStateId = "anual-1", Outcome = resultado, OrderId = resultado == "Success" ? "ord-1" : null
            }));

        // El estado Stopped no lo recogería el trabajador: se pone Active al sembrar y se para "durante" el proceso.
        if (estadoFinal == RecurringBillingStatus.Stopped)
        {
            var e = await db.SubscriptionBillingStates.SingleAsync();
            e.Status = RecurringBillingStatus.Active;
            await db.SaveChangesAsync();
            procesador.Setup(p => p.ProcessAsync("anual-1", false, It.IsAny<CancellationToken>()))
                .Callback(() => { e.Status = RecurringBillingStatus.Stopped; db.SaveChanges(); })
                .ReturnsAsync(Result<RecurringBillingProcessorResult>.Success(new RecurringBillingProcessorResult
                {
                    BillingStateId = "anual-1", Outcome = resultado
                }));
        }

        var r = await new RecurringChargeWorker(db, procesador.Object, Reloj(), new Mock<ILogger<RecurringChargeWorker>>().Object)
            .ProcessShardAsync("trozo");

        r.IsSuccess.Should().BeTrue();
        (await db.PointDeltaEvents.CountAsync()).Should().Be(0);
        (await db.CommissionTriggerQueues.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task El_seeder_marca_el_plan_anual_y_no_el_de_viajes()
    {
        await using var db = TestDbContextFactory.Create();
        db.Products.Add(new Product { Id = "prod-amb", Name = "Lifestyle Ambassador", Description = "", ImageUrl = "", MembershipLevelId = 1, MonthlyFee = 0m, SetupFee = 99m, AnnualPrice = 99m, IsActive = true, CreatedBy = "prueba" });
        await db.SaveChangesAsync();

        await RecurringBillingSeeder.SeedAsync(db, NullLogger.Instance);

        (await db.RecurringBillingPlans.SingleAsync(p => p.Name == "Lifestyle Ambassador (Annual)")).IsAnnualFeePlan.Should().BeTrue();
        (await db.RecurringBillingPlans.SingleAsync(p => p.Name == "Travel Advantage")).IsAnnualFeePlan.Should().BeFalse();
    }

    [Fact]
    public async Task El_seeder_marca_el_plan_anual_que_ya_existia_sin_marca()
    {
        await using var db = TestDbContextFactory.Create();
        db.RecurringBillingPlans.Add(new RecurringBillingPlan
        {
            Name = "Lifestyle Ambassador (Annual)", CycleType = RecurringCycleType.AnnualFromLastBilling,
            RetryCadenceDays = "1,1,1,2,2,5,5", OnAllRetriesFail = RecurringFailurePolicy.MarkExpired,
            IsActive = true, IsAnnualFeePlan = false, CreatedBy = "seed"
        });
        await db.SaveChangesAsync();

        await RecurringBillingSeeder.SeedAsync(db, NullLogger.Instance);

        (await db.RecurringBillingPlans.SingleAsync(p => p.Name == "Lifestyle Ambassador (Annual)")).IsAnnualFeePlan.Should().BeTrue();
    }

    [Fact]
    public async Task El_procesador_salta_un_estado_de_cuota_anual_sin_tocar_nada()
    {
        // Hasta que el Bloque 3 delegue en el cobro de la cuota, el motor no hace nada con estos estados:
        // ni pedido ni intento ni cambios en el estado.
        await using var db = await ConPlanesAsync();
        db.SubscriptionBillingStates.Add(new SubscriptionBillingState
        {
            Id = "anual-1", Purpose = BillingStatePurpose.AnnualFee, MembershipSubscriptionId = null, MemberId = "AMB-1",
            RecurringBillingPlanId = 2, BillingAnchorDate = Ahora.AddYears(-1), NextBillingDate = Ahora.Date,
            NextAttemptDate = Ahora.Date, Status = RecurringBillingStatus.Active, CreatedBy = "prueba", CreationDate = Ahora
        });
        await db.SaveChangesAsync();

        var procesador = new RecurringBillingProcessor(
            db, new Mock<ICommissionBalanceService>().Object, new RecurringBillingScheduler(),
            new Mock<MLMConquerorGlobalEdition.Billing.Services.Routing.IGatewayRouter>().Object,
            new Mock<MLMConquerorGlobalEdition.Billing.Services.Routing.IGatewayChargeOrchestrator>().Object,
            new Mock<MLMConquerorGlobalEdition.Billing.Services.Routing.ICardBrandDetector>().Object,
            new TransactionOnlyGate(db), Reloj(), NullLogger<RecurringBillingProcessor>.Instance);

        var r = await procesador.ProcessAsync("anual-1", forceBillNow: true);

        r.IsSuccess.Should().BeTrue();
        r.Value!.Outcome.Should().Be("Skipped");
        (await db.Orders.CountAsync()).Should().Be(0);
        (await db.RecurringBillingAttempts.CountAsync()).Should().Be(0);
        var estado = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();
        estado.NextAttemptDate.Should().Be(Ahora.Date);
        estado.Status.Should().Be(RecurringBillingStatus.Active);
    }
}
