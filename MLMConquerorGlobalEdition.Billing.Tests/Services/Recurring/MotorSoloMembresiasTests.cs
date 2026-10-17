using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Jobs;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

/// <summary>
/// Plan K (revisión M2): hasta que el Bloque 3 conecte el cobro de la cuota, el planificador, los
/// trabajadores y el barrido solo recogen estados de membresía. Un estado de cuota anual vencido no
/// entra en ningún lote ni llega al procesador.
/// </summary>
public class MotorSoloMembresiasTests
{
    private static readonly DateTime Hoy = new(2026, 10, 20, 5, 0, 0);

    private static IDateTimeProvider Reloj()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(Hoy);
        return m.Object;
    }

    private static async Task<AppDbContext> SembrarAsync()
    {
        var db = TestDbContextFactory.Create();
        db.SubscriptionBillingStates.AddRange(
            new SubscriptionBillingState
            {
                Id = "membresia", Purpose = BillingStatePurpose.Membership, MembershipSubscriptionId = "sub-1", MemberId = "AMB-1",
                RecurringBillingPlanId = 1, BillingAnchorDate = Hoy.AddDays(-30), NextBillingDate = Hoy.Date,
                NextAttemptDate = Hoy.Date, Status = RecurringBillingStatus.Active, ShardKey = 1, CreatedBy = "prueba"
            },
            new SubscriptionBillingState
            {
                Id = "cuota", Purpose = BillingStatePurpose.AnnualFee, MembershipSubscriptionId = null, MemberId = "AMB-2",
                RecurringBillingPlanId = 2, BillingAnchorDate = Hoy.AddYears(-1), NextBillingDate = Hoy.Date,
                NextAttemptDate = Hoy.Date, Status = RecurringBillingStatus.Active, ShardKey = 2, CreatedBy = "prueba"
            });
        await db.SaveChangesAsync();
        return db;
    }

    private static Mock<IRecurringBillingProcessor> Procesador()
    {
        var p = new Mock<IRecurringBillingProcessor>();
        p.Setup(x => x.ProcessAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync((string id, bool _, CancellationToken _) => Result<RecurringBillingProcessorResult>.Success(
             new RecurringBillingProcessorResult { BillingStateId = id, Outcome = "Skipped" }));
        return p;
    }

    [Fact]
    public async Task El_planificador_solo_cuenta_estados_de_membresia()
    {
        await using var db = await SembrarAsync();
        var ruta = new Mock<IGatewayRouter>();
        ruta.Setup(r => r.ResolveAsync(It.IsAny<GatewayRoutingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GatewayRoutingPlan>.Success(new GatewayRoutingPlan
            {
                RouteBucketKey = "b", Steps = new List<GatewayAttemptPlan> { new() { CardProcessor = CardProcessor.NmiSpreedly } }
            }));

        var r = await new RecurringBillingPlanner(db, ruta.Object, Reloj(), Mock.Of<ILogger<RecurringBillingPlanner>>()).PlanAsync(Hoy.Date);

        r.IsSuccess.Should().BeTrue();
        r.Value!.TotalCases.Should().Be(1);
    }

    [Fact]
    public async Task El_barrido_no_pasa_al_procesador_los_estados_de_cuota()
    {
        await using var db = await SembrarAsync();
        var p = Procesador();

        await new RecurringBillingSweepJob(db, p.Object, new RecurringBillingEnrollmentService(db, Reloj()), Reloj(),
            NullLogger<RecurringBillingSweepJob>.Instance).ExecuteAsync();

        p.Verify(x => x.ProcessAsync("membresia", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        p.Verify(x => x.ProcessAsync("cuota", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task El_trabajador_no_pasa_al_procesador_los_estados_de_cuota()
    {
        await using var db = await SembrarAsync();
        db.RecurringBillingBatches.Add(new RecurringBillingBatch
        {
            Id = "lote", RunDate = Hoy.Date, Gateway = CardProcessor.NmiSpreedly, WorkerCount = 1,
            ScheduledStartTime = Hoy, CaseCount = 2, Status = RecurringBillingBatchStatus.Planned, CreatedBy = "prueba"
        });
        db.RecurringBillingBatchShards.Add(new RecurringBillingBatchShard
        {
            Id = "trozo", BatchId = "lote", ShardIndex = 0, IdRangeStart = 1, IdRangeEnd = 10,
            Status = RecurringBillingBatchStatus.Planned, CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();
        var p = Procesador();

        await new RecurringChargeWorker(db, p.Object, Reloj(), Mock.Of<ILogger<RecurringChargeWorker>>()).ProcessShardAsync("trozo");

        p.Verify(x => x.ProcessAsync("membresia", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        p.Verify(x => x.ProcessAsync("cuota", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
