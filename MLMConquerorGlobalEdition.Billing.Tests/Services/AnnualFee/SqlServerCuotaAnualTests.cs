using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Billing.Tests.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// Lo que InMemory no puede probar del cobro de la cuota (Plan K, R14): el índice único de pagos vivos
/// por ciclo y dos "Pagar ahora" simultáneos del mismo miembro con el candado real. El índice único por
/// miembro y el CHECK del propósito están en SqlServerEsquemaCuotaAnualTests. Cada prueba usa un miembro
/// sintético propio, una fecha fija y borra lo que escribió. Se saltan sin SQL Server.
/// </summary>
public class SqlServerCuotaAnualTests : IAsyncLifetime
{
    // MemberProfiles.MemberId es nvarchar(20).
    private readonly string _member = $"TSK-{Guid.NewGuid():N}"[..20];
    private static readonly DateTime Ahora = new(2026, 10, 20, 9, 0, 0);

    public Task InitializeAsync() => Task.CompletedTask;

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
                // Orders sin índice por MemberId: los ids se leen sin bloqueos y se borra por clave.
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET DEADLOCK_PRIORITY HIGH;
                    DECLARE @o TABLE (Id nvarchar(450) PRIMARY KEY);
                    INSERT @o SELECT Id FROM Orders WITH (NOLOCK) WHERE MemberId = {m};
                    DELETE FROM AnnualFeePayments WHERE MemberId = {m};
                    DELETE FROM RecurringBillingAttempts WHERE MemberId = {m};
                    DELETE FROM TokenTransactions WHERE MemberId = {m} OR OriginalOwnerMemberId = {m};
                    DELETE FROM TokenBalances WHERE MemberId = {m};
                    DELETE FROM PaymentHistories WHERE MemberId = {m};
                    DELETE FROM CommissionEarnings WHERE BeneficiaryMemberId = {m};
                    DELETE x FROM Orders x JOIN @o o ON o.Id = x.Id;
                    DELETE FROM SubscriptionBillingStates WHERE MemberId = {m};
                    DELETE FROM MemberProfiles WHERE MemberId = {m};");
                return;
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 1205 or -2 && attempt < 8)
            {
                await Task.Delay(500 * attempt);
            }
        }
    }

    private static async Task<int> PlanAnualAsync(AppDbContext db) =>
        (await db.RecurringBillingPlans.AsNoTracking().SingleAsync(p => p.IsAnnualFeePlan)).Id;

    private SubscriptionBillingState EstadoAnual(int plan) => new()
    {
        Purpose = BillingStatePurpose.AnnualFee, MembershipSubscriptionId = null, MemberId = _member,
        RecurringBillingPlanId = plan, BillingAnchorDate = Ahora.Date.AddYears(-1), NextBillingDate = Ahora.Date,
        NextAttemptDate = Ahora.Date, Status = RecurringBillingStatus.Active, CreatedBy = "prueba-sql",
        CreationDate = Ahora, LastUpdateDate = Ahora
    };

    [SqlServerFact]
    public async Task Un_ciclo_no_admite_dos_pagos_vivos_pero_si_uno_vivo_y_otro_fallido()
    {
        await using var db = Db();
        var estado = EstadoAnual(await PlanAnualAsync(db));
        db.SubscriptionBillingStates.Add(estado);
        await db.SaveChangesAsync();

        AnnualFeePayment Pago(AnnualFeePaymentStatus s) => new()
        {
            MemberId = _member, SubscriptionBillingStateId = estado.Id, CycleDueDate = Ahora.Date, Amount = 99m,
            Method = AnnualFeePaymentMethod.EWallet, Source = AnnualFeePaymentSource.Automatic, Status = s,
            OrderId = Guid.NewGuid().ToString(), CreatedBy = "prueba-sql", CreationDate = Ahora, LastUpdateDate = Ahora
        };

        db.AnnualFeePayments.Add(Pago(AnnualFeePaymentStatus.Failed));
        db.AnnualFeePayments.Add(Pago(AnnualFeePaymentStatus.NeedsReconciliation));
        await db.SaveChangesAsync();

        await using var otra = Db();
        otra.AnnualFeePayments.Add(Pago(AnnualFeePaymentStatus.Pending));
        (await FluentActions.Awaiting(() => otra.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException!.Message.Should().Contain("UX_AnnualFeePayments_LivePerCycle");
    }

    [SqlServerFact]
    public async Task Dos_Pagar_ahora_a_la_vez_con_claves_distintas_cobran_una_sola_vez()
    {
        await using (var semilla = Db())
        {
            semilla.MemberProfiles.Add(new MemberProfile
            {
                UserId = Guid.NewGuid(), MemberId = _member, Email = $"{_member.ToLowerInvariant()}@prueba.test",
                FirstName = "Prueba", LastName = "Cuota", Country = "US", MemberType = MemberType.Ambassador,
                Status = MemberAccountStatus.Active, EnrollDate = Ahora.Date.AddYears(-1), FeesFromBalance = true,
                CreatedBy = "prueba-sql", CreationDate = Ahora, LastUpdateDate = Ahora
            });
            semilla.SubscriptionBillingStates.Add(EstadoAnual(await PlanAnualAsync(semilla)));
            for (var i = 0; i < 2; i++)
                semilla.TokenTransactions.Add(new TokenTransaction
                {
                    MemberId = _member, TokenTypeId = AnnualFeeRules.AnnualBizCenterTokenTypeId,
                    TransactionType = TokenTransactionType.Purchased, Quantity = 1,
                    ReferenceId = $"K{Guid.NewGuid():N}"[..16].ToUpperInvariant(), OriginalOwnerMemberId = _member,
                    Status = TokenInstanceStatus.Issued, CreatedBy = "prueba-sql", CreationDate = Ahora.AddDays(-i - 1)
                });
            await semilla.SaveChangesAsync();
        }

        async Task<string?> PagarAsync(string clave)
        {
            await using var db = Db();
            var reloj = CuotaAnualDb.Reloj(Ahora);
            var servicio = new AnnualFeeChargeService(
                db, reloj, new SqlServerMemberFundsGate(db, lockTimeoutMs: 120_000), new CommissionBalanceService(db, reloj),
                new Mock<IEWalletFunds>().Object, new AnnualFeeTokenConsumer(db),
                new AnnualFeeStateEnrollment(db, reloj, NullLogger<AnnualFeeStateEnrollment>.Instance),
                new RecurringBillingScheduler(), new ConfigurationBuilder().Build(), NullLogger<AnnualFeeChargeService>.Instance);
            var r = await servicio.PayNowAsync(new AnnualFeePayNowInput(_member, "prueba-sql",
                new PayAnnualFeeRequest { Method = AnnualFeeMethods.Token, IdempotencyKey = clave }));
            return r.IsSuccess ? null : r.ErrorCode;
        }

        var resultados = await Task.WhenAll(PagarAsync("clave-a"), PagarAsync("clave-b"));

        resultados.Count(e => e is null).Should().Be(1);
        resultados.Single(e => e is not null).Should().BeOneOf(AnnualFeeErrors.NotDue, AnnualFeeErrors.AlreadyPaid);

        await using var lectura = Db();
        (await lectura.AnnualFeePayments.CountAsync(p => p.MemberId == _member && p.Status == AnnualFeePaymentStatus.Completed))
            .Should().Be(1);
        (await AnnualFeeTokenQueries.Available(lectura, _member, Ahora).CountAsync()).Should().Be(1);
    }
}
