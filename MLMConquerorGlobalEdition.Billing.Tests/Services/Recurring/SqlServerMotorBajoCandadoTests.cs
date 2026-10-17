using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

/// <summary>
/// La renovación con comisiones bajo el candado real (Plan K, K18). InMemory no tiene transacciones
/// ni sp_getapplock: solo aquí se ve que un fallo dentro deshace el débito ya escrito, que el éxito se
/// confirma y que otra operación con el candado tomado deja la renovación para la siguiente pasada.
/// Usa un miembro y un plan sintéticos propios y borra lo que escribe.
/// </summary>
public class SqlServerMotorBajoCandadoTests : IAsyncLifetime
{
    // MemberProfiles.MemberId es nvarchar(20).
    private readonly string _member = $"TSK-{Guid.NewGuid():N}"[..20];
    private readonly string _planName = $"test-plan-k-{Guid.NewGuid():N}";
    private const int NivelElite = 3;

    /// <summary>Fecha fija (revisión M7): la prueba no depende del día en que se ejecute.</summary>
    private static readonly DateTime Ahora = new(2026, 10, 20, 9, 0, 0);

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Con tiempo de comando amplio: la base es la de desarrollo, compartida con los servicios levantados,
    /// y el procesador recorre Orders sin índice para comprobar el OrderNo (Plan K, R11).
    /// </summary>
    private static AppDbContext Db() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(SqlServerTestDb.ConnectionString, o => o.CommandTimeout(300)).Options);

    public async Task DisposeAsync()
    {
        if (!SqlServerTestDb.Reachable) return;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var db = Db();
                var m = _member;
                var p = _planName;
                // Orders no tiene índice por MemberId: los ids se leen sin bloqueos y se borra por clave,
                // para no esperar detrás de los servicios levantados que escriben en Orders.
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET DEADLOCK_PRIORITY HIGH;
                    DECLARE @o TABLE (Id nvarchar(450) PRIMARY KEY);
                    INSERT @o SELECT Id FROM Orders WITH (NOLOCK) WHERE MemberId = {m};
                    DELETE FROM RecurringBillingAttempts WHERE MemberId = {m};
                    DELETE FROM SubscriptionBillingStates WHERE MemberId = {m};
                    DELETE FROM MembershipSubscriptions WHERE MemberId = {m};
                    DELETE d FROM OrderDetails d JOIN @o o ON o.Id = d.OrderId;
                    DELETE FROM PaymentHistories WHERE MemberId = {m};
                    DELETE x FROM Orders x JOIN @o o ON o.Id = x.Id;
                    DELETE FROM TokenTransactions WHERE MemberId = {m};
                    DELETE FROM TokenBalances WHERE MemberId = {m};
                    DELETE FROM CommissionEarnings WHERE BeneficiaryMemberId = {m};
                    DELETE FROM MemberProfiles WHERE MemberId = {m};
                    DELETE FROM RecurringBillingPlans WHERE Name = {p};");
                return;
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 1205 or -2 && attempt < 8)
            {
                await Task.Delay(500 * attempt);
            }
        }
    }

    private async Task<string> SembrarAsync(int? tipoDeToken)
    {
        await using var db = Db();
        var now = Ahora;
        db.MemberProfiles.Add(new MemberProfile
        {
            MemberId = _member, UserId = Guid.NewGuid(), Email = $"{_member}@example.com", FirstName = "Prueba", LastName = "PlanK",
            MemberType = MemberType.Ambassador, Status = MemberAccountStatus.Active, EnrollDate = now.AddYears(-1),
            Country = "US", CreatedBy = "sql-test", CreationDate = now, LastUpdateDate = now
        });
        var plan = new RecurringBillingPlan
        {
            Name = _planName, CycleType = RecurringCycleType.Every30Days, RetryCadenceDays = "1,2",
            OnAllRetriesFail = RecurringFailurePolicy.RetryOnMonthlyAnniversary, StopAfterUnbilledDays = 90,
            PayFromCommissionBalanceFirst = true, TokenTypeId = tipoDeToken, IsActive = false,
            CreatedBy = "sql-test", CreationDate = now, LastUpdateDate = now
        };
        db.RecurringBillingPlans.Add(plan);
        var sub = new MembershipSubscription
        {
            MemberId = _member, MembershipLevelId = NivelElite, SubscriptionStatus = MembershipStatus.Active,
            StartDate = now.AddDays(-30), CreatedBy = "sql-test", CreationDate = now, LastUpdateDate = now
        };
        db.MembershipSubscriptions.Add(sub);
        db.CommissionEarnings.Add(new CommissionEarning
        {
            BeneficiaryMemberId = _member, CommissionTypeId = 1, Amount = 500m, Status = CommissionEarningStatus.Pending,
            EarnedDate = now.AddDays(-10), PaymentDate = now.AddDays(-1), IsManualEntry = true, Notes = "sql-test",
            CreatedBy = "sql-test", CreationDate = now, LastUpdateDate = now
        });
        await db.SaveChangesAsync();

        var state = new SubscriptionBillingState
        {
            MembershipSubscriptionId = sub.Id, MemberId = _member, RecurringBillingPlanId = plan.Id,
            BillingAnchorDate = now.AddDays(-30), NextBillingDate = now.Date, NextAttemptDate = now.Date,
            Status = RecurringBillingStatus.Active, CreatedBy = "sql-test", CreationDate = now, LastUpdateDate = now
        };
        db.SubscriptionBillingStates.Add(state);
        await db.SaveChangesAsync();
        return state.Id;
    }

    private static RecurringBillingProcessor Procesador(AppDbContext db, int lockTimeoutMs = 120_000)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(r => r.Now).Returns(Ahora);
        var ruta = new Mock<IGatewayRouter>();
        ruta.Setup(r => r.ResolveAsync(It.IsAny<GatewayRoutingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GatewayRoutingPlan>.Failure("NO_ROUTE", "Sin ruta en la prueba."));
        return new RecurringBillingProcessor(
            db, new CommissionBalanceService(db, reloj.Object), new RecurringBillingScheduler(),
            ruta.Object, new Mock<IGatewayChargeOrchestrator>().Object, new Mock<ICardBrandDetector>().Object,
            new SqlServerMemberFundsGate(db, lockTimeoutMs), new Mock<MLMConquerorGlobalEdition.Billing.Services.AnnualFee.IAnnualFeeChargeService>().Object, reloj.Object, NullLogger<RecurringBillingProcessor>.Instance);
    }

    /// <summary>
    /// Orders no tiene índice por MemberId (Plan K, R11) y los servicios levantados escriben en ella: una
    /// lectura con bloqueos se queda esperando. NOLOCK basta para mirar el pedido propio ya confirmado.
    /// </summary>
    private async Task<List<OrderStatus>> EstadosDePedidosAsync()
    {
        await using var db = Db();
        var estados = await db.Database
            .SqlQuery<int>($"SELECT [Status] AS [Value] FROM [Orders] WITH (NOLOCK) WHERE [MemberId] = {_member}")
            .ToListAsync();
        return estados.Select(e => (OrderStatus)e).ToList();
    }

    /// <summary>
    /// Procesa el estado. Si el procesador sale víctima de un interbloqueo con los servicios levantados
    /// (pasa en el AnyAsync del OrderNo, que recorre Orders sin índice, antes de tocar dinero), la base ya
    /// deshizo lo suyo: se cancela el pedido que hubiera quedado a medias y se repite.
    /// </summary>
    private async Task<Result<RecurringBillingProcessorResult>> ProcesarAsync(string stateId, int lockTimeoutMs = 120_000)
    {
        for (var intento = 1; ; intento++)
        {
            await using var db = Db();
            try
            {
                var r = await Procesador(db, lockTimeoutMs).ProcessAsync(stateId);
                db.Database.CurrentTransaction.Should().BeNull();
                return r;
            }
            catch (Exception ex) when (EsInterbloqueo(ex) && intento < 8)
            {
                await using var limpieza = Db();
                await limpieza.Database.ExecuteSqlInterpolatedAsync($@"
                    DECLARE @o TABLE (Id nvarchar(450) PRIMARY KEY);
                    INSERT @o SELECT Id FROM Orders WITH (NOLOCK) WHERE MemberId = {_member} AND Status = 3;
                    UPDATE x SET Status = 5 FROM Orders x JOIN @o o ON o.Id = x.Id;");
                await Task.Delay(300 * intento);
            }
        }
    }

    private static bool EsInterbloqueo(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
            if (ex is Microsoft.Data.SqlClient.SqlException { Number: 1205 }) return true;
        return false;
    }

    private async Task<int> DebitosAsync()
    {
        await using var db = Db();
        return await db.CommissionEarnings.CountAsync(e => e.BeneficiaryMemberId == _member && e.Amount < 0);
    }

    [SqlServerFact]
    public async Task Con_saldo_debita_y_confirma_la_renovacion()
    {
        var stateId = await SembrarAsync(tipoDeToken: 100);

        var r = await ProcesarAsync(stateId);
        r.IsSuccess.Should().BeTrue(r.Error);
        r.Value!.FundingSource.Should().Be("CommissionBalance");

        (await DebitosAsync()).Should().Be(1);
        await using var check = Db();
        (await check.SubscriptionBillingStates.AsNoTracking().SingleAsync(s => s.Id == stateId))
            .LastSuccessfulBillingDate.Should().NotBeNull();
        (await EstadosDePedidosAsync()).Should().ContainSingle(s => s == OrderStatus.Paid);
    }

    [SqlServerFact]
    public async Task Un_fallo_dentro_del_candado_deshace_el_debito_y_cancela_el_pedido()
    {
        // Plan sin tipo de token: FundWithCommissionBalanceAsync guarda el débito y después falla. Antes
        // el débito quedaba escrito con el pedido cancelado y sin renovar.
        var stateId = await SembrarAsync(tipoDeToken: null);

        var r = await ProcesarAsync(stateId);
        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be("NO_TOKEN_TYPE");

        (await DebitosAsync()).Should().Be(0);
        await using var check = Db();
        (await EstadosDePedidosAsync()).Should().NotBeEmpty().And.OnlyContain(s => s == OrderStatus.Cancelled);
        (await check.PaymentHistories.CountAsync(p => p.MemberId == _member)).Should().Be(0);
        (await check.SubscriptionBillingStates.AsNoTracking().SingleAsync(s => s.Id == stateId))
            .LastSuccessfulBillingDate.Should().BeNull();
    }

    [SqlServerFact]
    public async Task Con_el_candado_tomado_por_otra_operacion_no_debita_y_queda_para_la_siguiente_pasada()
    {
        var stateId = await SembrarAsync(tipoDeToken: 100);

        await using var otra = Db();
        await using var txOtra = await new SqlServerMemberFundsGate(otra).EnterAsync(_member);

        var r = await ProcesarAsync(stateId, lockTimeoutMs: 300);
        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(RecurringBillingProcessor.MemberFundsBusyCode);

        await txOtra.RollbackAsync();
        (await DebitosAsync()).Should().Be(0);
        await using var check = Db();
        (await EstadosDePedidosAsync()).Should().NotBeEmpty().And.OnlyContain(s => s == OrderStatus.Cancelled);
        (await check.SubscriptionBillingStates.AsNoTracking().SingleAsync(s => s.Id == stateId))
            .NextAttemptDate.Should().Be(Ahora.Date);
    }
}
