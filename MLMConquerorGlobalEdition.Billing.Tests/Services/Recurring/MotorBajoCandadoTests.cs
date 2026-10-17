using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

/// <summary>
/// Plan K (K18): la renovación de Travel Advantage pagada con comisiones toma el candado de fondos del
/// miembro y mira el saldo GASTABLE, igual que la compra de tokens (aprobado por Nick, K18): sin las
/// comisiones reservadas por un pago en curso ni las que aún no son pagables. Sin candado, un pago y
/// una renovación podían gastar las mismas comisiones a la vez.
/// </summary>
public class MotorBajoCandadoTests
{
    private static readonly DateTime Ahora = new(2026, 10, 20, 9, 0, 0);
    private const string Miembro = "AMB-000001";

    private static async Task<AppDbContext> SembrarAsync(decimal comision, DateTime pagableDesde, int? tipoDeToken = 100)
    {
        var db = TestDbContextFactory.Create();
        db.RecurringBillingPlans.Add(new RecurringBillingPlan
        {
            Id = 1, Name = "Travel Advantage", CycleType = RecurringCycleType.Every30Days, RetryCadenceDays = "1,2",
            OnAllRetriesFail = RecurringFailurePolicy.RetryOnMonthlyAnniversary, StopAfterUnbilledDays = 90,
            PayFromCommissionBalanceFirst = true, TokenTypeId = tipoDeToken, IsActive = true, CreatedBy = "prueba"
        });
        db.MembershipLevels.Add(new MembershipLevel { Id = 3, Name = "Travel Advantage - Elite", SortOrder = 3, CreatedBy = "prueba" });
        db.Products.Add(new Product
        {
            Id = "prod-elite", Name = "Travel Advantage Elite", Description = "", ImageUrl = "", MembershipLevelId = 3,
            MonthlyFee = 119.97m, SetupFee = 0m, IsActive = true, CreatedBy = "prueba"
        });
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = "sub-1", MemberId = Miembro, MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.Active,
            StartDate = Ahora.AddDays(-30), CreatedBy = "prueba"
        });
        db.SubscriptionBillingStates.Add(new SubscriptionBillingState
        {
            Id = "estado-1", MembershipSubscriptionId = "sub-1", MemberId = Miembro, RecurringBillingPlanId = 1,
            BillingAnchorDate = Ahora.AddDays(-30), NextBillingDate = Ahora.Date, NextAttemptDate = Ahora.Date,
            Status = RecurringBillingStatus.Active, CreatedBy = "prueba"
        });
        db.CommissionEarnings.Add(new CommissionEarning
        {
            BeneficiaryMemberId = Miembro, CommissionTypeId = 1, Amount = comision, Status = CommissionEarningStatus.Pending,
            EarnedDate = pagableDesde.AddDays(-7), PaymentDate = pagableDesde, CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static RecurringBillingProcessor Procesador(AppDbContext db, IMemberFundsGate candado)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(r => r.Now).Returns(Ahora);
        var ruta = new Mock<IGatewayRouter>();
        ruta.Setup(r => r.ResolveAsync(It.IsAny<GatewayRoutingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GatewayRoutingPlan>.Failure("NO_ROUTE", "Sin ruta en la prueba."));

        return new RecurringBillingProcessor(
            db, new CommissionBalanceService(db, reloj.Object), new RecurringBillingScheduler(),
            ruta.Object, new Mock<IGatewayChargeOrchestrator>().Object, new Mock<ICardBrandDetector>().Object,
            candado, new Mock<MLMConquerorGlobalEdition.Billing.Services.AnnualFee.IAnnualFeeChargeService>().Object, reloj.Object, NullLogger<RecurringBillingProcessor>.Instance);
    }

    [Fact]
    public async Task Con_saldo_paga_con_comisiones_dentro_del_candado()
    {
        await using var db = await SembrarAsync(200m, Ahora.AddDays(-1));
        var candado = new TransactionOnlyGate(db);

        var r = await Procesador(db, candado).ProcessAsync("estado-1");

        r.IsSuccess.Should().BeTrue();
        r.Value!.Outcome.Should().Be("Success");
        r.Value.FundingSource.Should().Be("CommissionBalance");
        candado.Entered.Should().Equal(Miembro);
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Paid);
        (await db.CommissionEarnings.SingleAsync(e => e.Amount < 0)).Amount.Should().Be(-119.97m);
    }

    [Fact]
    public async Task Las_comisiones_que_aun_no_son_pagables_no_pagan_la_renovacion()
    {
        await using var db = await SembrarAsync(200m, Ahora.AddDays(5));

        var r = await Procesador(db, new TransactionOnlyGate(db)).ProcessAsync("estado-1");

        // Sin saldo gastable pasa a la tarjeta, que está apagada: fallo con cadencia.
        r.Value!.Outcome.Should().Be("Failed");
        r.Value.FailureReason.Should().StartWith(RecurringCardSwitch.CardDisabledCode);
        (await db.CommissionEarnings.CountAsync(e => e.Amount < 0)).Should().Be(0);
    }

    [Theory]
    [InlineData(PayoutOutcome.Pending, false)]
    [InlineData(PayoutOutcome.Success, false)]
    [InlineData(PayoutOutcome.Failed, true)]
    public async Task Las_comisiones_reservadas_por_un_pago_no_fallido_no_pagan_la_renovacion(string resultado, bool paga)
    {
        await using var db = await SembrarAsync(200m, Ahora.AddDays(-1));
        var comision = await db.CommissionEarnings.SingleAsync();
        var pago = new PayoutAttempt
        {
            MemberId = Miembro, AmountUsd = 200m, Outcome = resultado, PayoutAccountSnapshot = "x",
            ProcessDateUtc = Ahora, AttemptedAtUtc = Ahora, EarningsCount = 1, CreatedBy = "prueba"
        };
        db.PayoutAttempts.Add(pago);
        await db.SaveChangesAsync();
        db.PayoutAttemptEarnings.Add(new PayoutAttemptEarning
        {
            PayoutAttemptId = pago.Id, CommissionEarningId = comision.Id, Amount = 200m, CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();

        var r = await Procesador(db, new TransactionOnlyGate(db)).ProcessAsync("estado-1");

        (r.Value!.Outcome == "Success").Should().Be(paga);
        (await db.CommissionEarnings.CountAsync(e => e.Amount < 0)).Should().Be(paga ? 1 : 0);
    }

    [Fact]
    public async Task Sin_saldo_suficiente_mira_dentro_del_candado_y_pasa_a_la_tarjeta_sin_debitar()
    {
        await using var db = await SembrarAsync(50m, Ahora.AddDays(-1));
        var candado = new TransactionOnlyGate(db);

        var r = await Procesador(db, candado).ProcessAsync("estado-1");

        // Sin saldo pasa a la tarjeta, que está apagada: fallo con cadencia.
        r.Value!.Outcome.Should().Be("Failed");
        r.Value.FailureReason.Should().StartWith(RecurringCardSwitch.CardDisabledCode);
        candado.Entered.Should().Equal(Miembro);
        (await db.CommissionEarnings.CountAsync(e => e.Amount < 0)).Should().Be(0);
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task Con_el_candado_ocupado_no_debita_ni_avanza_y_cancela_el_pedido()
    {
        await using var db = await SembrarAsync(200m, Ahora.AddDays(-1));

        var r = await Procesador(db, new BusyGate()).ProcessAsync("estado-1");

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(RecurringBillingProcessor.MemberFundsBusyCode);
        (await db.CommissionEarnings.CountAsync(e => e.Amount < 0)).Should().Be(0);
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Cancelled);
        (await db.RecurringBillingAttempts.CountAsync()).Should().Be(0);

        var estado = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();
        estado.NextAttemptDate.Should().Be(Ahora.Date);
        estado.CurrentAttemptIndex.Should().Be(0);
        estado.LastSuccessfulBillingDate.Should().BeNull();
    }

    [Fact]
    public async Task Un_fallo_dentro_del_candado_cancela_el_pedido_sin_soltar_lo_que_el_trabajador_tiene_cargado()
    {
        // Plan sin tipo de token: FundWithCommissionBalanceAsync escribe el débito y luego falla. En SQL
        // Server el débito se deshace (SqlServerMotorBajoCandadoTests); InMemory no deshace. Aquí se
        // comprueba lo que InMemory sí ve: el pedido se cancela, el estado no avanza y lo que el
        // trabajador tenía cargado en el mismo contexto (su trozo) sigue rastreado para guardarlo al final.
        await using var db = await SembrarAsync(200m, Ahora.AddDays(-1), tipoDeToken: null);
        db.RecurringBillingBatches.Add(new RecurringBillingBatch
        {
            Id = "lote", RunDate = Ahora.Date, Gateway = CardProcessor.NmiSpreedly, WorkerCount = 1,
            ScheduledStartTime = Ahora, CaseCount = 1, Status = RecurringBillingBatchStatus.InProgress, CreatedBy = "prueba"
        });
        db.RecurringBillingBatchShards.Add(new RecurringBillingBatchShard
        {
            Id = "trozo", BatchId = "lote", ShardIndex = 0, IdRangeStart = 1, IdRangeEnd = 100,
            Status = RecurringBillingBatchStatus.InProgress, CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();
        var trozo = await db.RecurringBillingBatchShards.SingleAsync();

        var r = await Procesador(db, new TransactionOnlyGate(db)).ProcessAsync("estado-1");

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be("NO_TOKEN_TYPE");
        db.Entry(trozo).State.Should().Be(EntityState.Unchanged);
        (await db.Orders.AsNoTracking().SingleAsync()).Status.Should().Be(OrderStatus.Cancelled);
        var estado = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();
        estado.LastSuccessfulBillingDate.Should().BeNull();
        estado.NextAttemptDate.Should().Be(Ahora.Date);
    }

    /// <summary>Un candado cuya transacción falla al deshacer (conexión caída a mitad).</summary>
    private sealed class RollbackRotoGate(AppDbContext db) : IMemberFundsGate
    {
        public async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> EnterAsync(string memberId, CancellationToken ct = default)
            => new TxRota(await db.Database.BeginTransactionAsync(ct));

        public Task<bool> TryAcquireAsync(string memberId, TimeSpan timeout, CancellationToken ct = default) => Task.FromResult(true);

        private sealed class TxRota(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction inner)
            : Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction
        {
            public Guid TransactionId => inner.TransactionId;
            public void Commit() => inner.Commit();
            public Task CommitAsync(CancellationToken ct = default) => inner.CommitAsync(ct);
            public void Rollback() => throw new InvalidOperationException("Conexión caída al deshacer.");
            public Task RollbackAsync(CancellationToken ct = default) => throw new InvalidOperationException("Conexión caída al deshacer.");
            public void Dispose() => inner.Dispose();
            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }

    [Fact]
    public async Task Si_deshacer_falla_sin_haber_movido_nada_sigue_a_la_tarjeta_sin_romper()
    {
        // Revisión M6: un RollbackAsync que falla no tapa el resultado: sin saldo no se movió nada.
        await using var db = await SembrarAsync(50m, Ahora.AddDays(-1));

        var r = await Procesador(db, new RollbackRotoGate(db)).ProcessAsync("estado-1");

        r.Value!.Outcome.Should().Be("Failed");
        r.Value.FailureReason.Should().StartWith(RecurringCardSwitch.CardDisabledCode);
        (await db.CommissionEarnings.CountAsync(e => e.Amount < 0)).Should().Be(0);
    }

    [Fact]
    public async Task El_exito_registra_el_numero_de_intento_del_ciclo()
    {
        // Revisión M-5: antes se registraba 0 en los éxitos; los fallos ya iban 1, 2…
        await using var db = await SembrarAsync(200m, Ahora.AddDays(-1));
        var e = await db.SubscriptionBillingStates.SingleAsync();
        e.CurrentAttemptIndex = 1;
        e.Status = RecurringBillingStatus.Retrying;
        await db.SaveChangesAsync();

        await Procesador(db, new TransactionOnlyGate(db)).ProcessAsync("estado-1");

        (await db.RecurringBillingAttempts.SingleAsync()).AttemptIndex.Should().Be(2);
    }
}
