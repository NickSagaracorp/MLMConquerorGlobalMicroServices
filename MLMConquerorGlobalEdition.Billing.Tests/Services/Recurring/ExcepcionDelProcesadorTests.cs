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
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

/// <summary>
/// Revisión del Bloque 3 (C-1): si el procesador lanza a mitad de un caso, el trabajador y el barrido
/// descartan lo que haya quedado sin guardar en el contexto compartido antes de seguir. Si no, el
/// siguiente caso (o el cierre del trozo) guardaría un pedido o un estado a medias del caso que falló.
/// </summary>
public class ExcepcionDelProcesadorTests
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
        foreach (var (id, key) in new[] { ("falla", 1L), ("bien", 2L) })
            db.SubscriptionBillingStates.Add(new SubscriptionBillingState
            {
                Id = id, MembershipSubscriptionId = "sub-" + id, MemberId = "AMB-" + id, RecurringBillingPlanId = 1,
                BillingAnchorDate = Hoy.AddDays(-30), NextBillingDate = Hoy.Date, NextAttemptDate = Hoy.Date,
                Status = RecurringBillingStatus.Active, ShardKey = key, CreatedBy = "prueba"
            });
        await db.SaveChangesAsync();
        return db;
    }

    /// <summary>En "falla" deja un pedido y un cambio de estado sin guardar y lanza; en "bien" no hace nada.</summary>
    private static Mock<IRecurringBillingProcessor> Procesador(AppDbContext db)
    {
        var p = new Mock<IRecurringBillingProcessor>();
        p.Setup(x => x.ProcessAsync("falla", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
         .Returns(async () =>
         {
             db.Orders.Add(new Orders { MemberId = "AMB-falla", OrderNo = "X-1", TotalAmount = 1m, CreatedBy = "prueba" });
             var estado = await db.SubscriptionBillingStates.SingleAsync(s => s.Id == "falla");
             estado.NextBillingDate = Hoy.Date.AddYears(5);
             throw new InvalidOperationException("fallo a mitad");
         });
        p.Setup(x => x.ProcessAsync("bien", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<RecurringBillingProcessorResult>.Success(
             new RecurringBillingProcessorResult { BillingStateId = "bien", Outcome = "Skipped" }));
        return p;
    }

    private static async Task NadaDelCasoFallidoAsync(AppDbContext db)
    {
        await db.SaveChangesAsync();
        (await db.Orders.CountAsync()).Should().Be(0);
        (await db.SubscriptionBillingStates.AsNoTracking().SingleAsync(s => s.Id == "falla")).NextBillingDate.Should().Be(Hoy.Date);
    }

    [Fact]
    public async Task El_trabajador_descarta_lo_sucio_y_cierra_el_trozo()
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
        var p = Procesador(db);

        var r = await new RecurringChargeWorker(db, p.Object, Reloj(), Mock.Of<ILogger<RecurringChargeWorker>>()).ProcessShardAsync("trozo");

        r.IsSuccess.Should().BeTrue();
        p.Verify(x => x.ProcessAsync("bien", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        (await db.RecurringBillingBatchShards.AsNoTracking().SingleAsync()).Status.Should().Be(RecurringBillingBatchStatus.Done);
        await NadaDelCasoFallidoAsync(db);
    }

    [Fact]
    public async Task El_barrido_descarta_lo_sucio_y_sigue()
    {
        await using var db = await SembrarAsync();
        var p = Procesador(db);

        await new RecurringBillingSweepJob(db, p.Object, new RecurringBillingEnrollmentService(db, Reloj()), Reloj(),
            NullLogger<RecurringBillingSweepJob>.Instance).ExecuteAsync();

        p.Verify(x => x.ProcessAsync("bien", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        await NadaDelCasoFallidoAsync(db);
    }

    /// <summary>Logger que guarda nivel y mensaje para comprobar los avisos.</summary>
    private sealed class LoggerQueGuarda<T> : ILogger<T>
    {
        public readonly List<(LogLevel Nivel, string Mensaje)> Entradas = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entradas.Add((logLevel, formatter(state, exception)));
    }

    [Fact]
    public async Task Cobrado_pero_sin_eventos_deja_un_aviso_propio_y_sigue()
    {
        // Revisión final (M-d): el cobro ya está hecho; si los puntos y disparadores no se guardan, hay
        // que poder encontrarlo en el log y emitirlos a mano. El caso no cuenta como fallido.
        var fallos = new SaveFaults();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(fallos).Options);
        await using var _ = db;
        db.MembershipSubscriptions.Add(new MLMConquerorGlobalEdition.Domain.Entities.Membership.MembershipSubscription
        {
            Id = "sub-bien", MemberId = "AMB-bien", MembershipLevelId = 3, StartDate = Hoy, DualTeamContribution = 10,
            CreatedBy = "prueba"
        });
        db.SubscriptionBillingStates.Add(new SubscriptionBillingState
        {
            Id = "bien", MembershipSubscriptionId = "sub-bien", MemberId = "AMB-bien", RecurringBillingPlanId = 1,
            BillingAnchorDate = Hoy.AddDays(-30), NextBillingDate = Hoy.Date, NextAttemptDate = Hoy.Date,
            Status = RecurringBillingStatus.Active, ShardKey = 2, CreatedBy = "prueba"
        });
        db.RecurringBillingBatches.Add(new RecurringBillingBatch
        {
            Id = "lote", RunDate = Hoy.Date, Gateway = CardProcessor.NmiSpreedly, WorkerCount = 1,
            ScheduledStartTime = Hoy, CaseCount = 1, Status = RecurringBillingBatchStatus.Planned, CreatedBy = "prueba"
        });
        db.RecurringBillingBatchShards.Add(new RecurringBillingBatchShard
        {
            Id = "trozo", BatchId = "lote", ShardIndex = 0, IdRangeStart = 1, IdRangeEnd = 10,
            Status = RecurringBillingBatchStatus.Planned, CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();
        fallos.FailWhen(c => c.ChangeTracker.Entries<MLMConquerorGlobalEdition.Domain.Entities.Commission.PointDeltaEvent>()
            .Any(e => e.State == EntityState.Added), times: 1);
        var p = new Mock<IRecurringBillingProcessor>();
        p.Setup(x => x.ProcessAsync("bien", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<RecurringBillingProcessorResult>.Success(
             new RecurringBillingProcessorResult { BillingStateId = "bien", Outcome = "Success", OrderId = "ord-1" }));
        var log = new LoggerQueGuarda<RecurringChargeWorker>();

        var r = await new RecurringChargeWorker(db, p.Object, Reloj(), log).ProcessShardAsync("trozo");

        r.IsSuccess.Should().BeTrue();
        r.Value!.Succeeded.Should().Be(1);
        r.Value.Failed.Should().Be(0);
        log.Entradas.Should().Contain(e => e.Nivel == LogLevel.Critical
            && e.Mensaje.Contains("bien") && e.Mensaje.Contains("ord-1") && e.Mensaje.Contains("eventos"));
        (await db.RecurringBillingBatchShards.AsNoTracking().SingleAsync()).Status.Should().Be(RecurringBillingBatchStatus.Done);
        (await db.PointDeltaEvents.CountAsync()).Should().Be(0);
    }
}
