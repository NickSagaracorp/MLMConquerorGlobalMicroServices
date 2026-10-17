using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// Revisión del Bloque 3 (I-1, I-2, I-3, M-1). Lo que el cobro automático escribe en el estado sin
/// cobrar (sin fondos, exclusiones, bloqueos) va bajo el candado y releyendo: si mientras tanto alguien
/// pagó el ciclo, no se gasta un reintento ni se mueve nada. Un eWallet que no responde no es "sin
/// fondos". El cobro forzado también exige el interruptor.
/// </summary>
public class EscriturasDelEstadoDeLaCuotaTests
{
    private const string Miembro = "AMB-1";
    private static readonly DateTime Ahora = CuotaAnualDb.Ahora;
    private static readonly DateTime Vence = new(2027, 6, 5);

    /// <summary>Candado que, en la entrada número <paramref name="cual"/>, deja que "otro" haga algo antes.</summary>
    private sealed class GanchoGate(AppDbContext db, int cual, Func<Task> otro) : IMemberFundsGate
    {
        private int _n;

        public async Task<IDbContextTransaction> EnterAsync(string memberId, CancellationToken ct = default)
        {
            if (++_n == cual) await otro();
            return await db.Database.BeginTransactionAsync(ct);
        }

        public Task<bool> TryAcquireAsync(string memberId, TimeSpan timeout, CancellationToken ct = default) => Task.FromResult(true);
    }

    private static async Task<(AppDbContext Db, string Nombre)> ConMiembroAsync(bool conSaldo = false)
    {
        var nombre = Guid.NewGuid().ToString();
        var db = CuotaAnualDb.Nueva(nombre);
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, Miembro, conSaldo: conSaldo);
        await CuotaAnualDb.EstadoAsync(db, Miembro, Vence);
        return (db, nombre);
    }

    private static Task<SubscriptionBillingState> EstadoAsync(AppDbContext db) =>
        db.SubscriptionBillingStates.AsNoTracking().SingleAsync();

    [Fact]
    public async Task Si_alguien_paga_el_ciclo_antes_de_apuntar_el_fallo_no_se_gasta_un_reintento()
    {
        var (db, nombre) = await ConMiembroAsync(conSaldo: false);
        await using var _ = db;
        // Entrada 1: buscar token (no hay). Entrada 2: apuntar "sin fondos". Antes de esa, otro lo pagó.
        var candado = new GanchoGate(db, 2, async () =>
        {
            await using var otra = CuotaAnualDb.Nueva(nombre);
            var e = await otra.SubscriptionBillingStates.SingleAsync();
            e.NextBillingDate = new DateTime(2028, 6, 10);
            e.NextAttemptDate = new DateTime(2028, 6, 10);
            await otra.SaveChangesAsync();
        });

        var r = await CobroAutomaticoDeCuotaAnualTests.Servicio(db, candado: candado).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Skipped);
        (await db.RecurringBillingAttempts.CountAsync()).Should().Be(0);
        var estado = await EstadoAsync(db);
        estado.CurrentAttemptIndex.Should().Be(0);
        estado.NextBillingDate.Should().Be(new DateTime(2028, 6, 10));
    }

    [Fact]
    public async Task Sin_fondos_con_el_ciclo_aun_vencido_si_gasta_el_reintento()
    {
        var (db, _) = await ConMiembroAsync(conSaldo: false);
        await using var __ = db;
        var candado = new TransactionOnlyGate(db);

        var r = await CobroAutomaticoDeCuotaAnualTests.Servicio(db, candado: candado).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Failed);
        candado.Entered.Count.Should().Be(2); // buscar token y apuntar el fallo
        (await EstadoAsync(db)).CurrentAttemptIndex.Should().Be(1);
    }

    [Fact]
    public async Task Una_DbUpdateConcurrencyException_al_apuntar_el_fallo_no_rompe_ni_gasta_reintento()
    {
        var nombre = Guid.NewGuid().ToString();
        var fallos = new SaveFaults();
        await using var db = CuotaAnualDb.Nueva(nombre, fallos);
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, Miembro, conSaldo: false);
        await CuotaAnualDb.EstadoAsync(db, Miembro, Vence);
        fallos.FailWhen(c => c.ChangeTracker.Entries<RecurringBillingAttempt>().Any(e => e.State == EntityState.Added), times: 1,
            make: _ => new DbUpdateConcurrencyException("el estado cambió"));

        var r = await CobroAutomaticoDeCuotaAnualTests.Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Skipped);
        await db.SaveChangesAsync();
        (await db.RecurringBillingAttempts.CountAsync()).Should().Be(0);
        (await EstadoAsync(db)).CurrentAttemptIndex.Should().Be(0);
    }

    [Fact]
    public async Task Un_eWallet_que_no_responde_no_es_sin_fondos_y_se_mira_manana()
    {
        var (db, _) = await ConMiembroAsync(conSaldo: true);
        await using var __ = db;
        var ew = new Mock<IEWalletFunds>();
        ew.Setup(e => e.UserNameOfAsync(Miembro, It.IsAny<CancellationToken>())).ReturnsAsync("amb-1");
        ew.Setup(e => e.BalanceAsync("amb-1", It.IsAny<CancellationToken>()))
          .ReturnsAsync(Result<decimal>.Failure("TIMEOUT", "i-Payout no contestó"));

        var r = await CobroAutomaticoDeCuotaAnualTests.Servicio(db, ewallet: ew).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Skipped);
        r.Value.FailureReason.Should().Be(AnnualFeeErrors.EWalletUnavailable);
        (await db.RecurringBillingAttempts.CountAsync()).Should().Be(0);
        var estado = await EstadoAsync(db);
        estado.CurrentAttemptIndex.Should().Be(0);
        estado.NextAttemptDate.Should().Be(Ahora.Date.AddDays(1));
        estado.NextBillingDate.Should().Be(Vence);
    }

    [Fact]
    public async Task Con_el_interruptor_apagado_tampoco_se_cobra_forzado()
    {
        var (db, _) = await ConMiembroAsync();
        await using var __ = db;
        await CuotaAnualDb.TokenAsync(db, Miembro);

        var r = await CobroAutomaticoDeCuotaAnualTests.Servicio(db, encendido: false).ChargeDueAsync("anual-" + Miembro, force: true);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Skipped);
        r.Value.FailureReason.Should().Be(AnnualFeeErrors.AutoChargeDisabled);
        (await db.AnnualFeePayments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Bloqueado_por_un_pago_en_revision_se_mira_manana()
    {
        var (db, _) = await ConMiembroAsync(conSaldo: true);
        await using var __ = db;
        db.AnnualFeePayments.Add(new AnnualFeePayment
        {
            MemberId = Miembro, SubscriptionBillingStateId = "anual-" + Miembro, CycleDueDate = Vence, Amount = 99m,
            Method = AnnualFeePaymentMethod.EWallet, Source = AnnualFeePaymentSource.Automatic,
            Status = AnnualFeePaymentStatus.NeedsReconciliation, OrderId = "ord-antes", CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();

        var r = await CobroAutomaticoDeCuotaAnualTests.Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.FailureReason.Should().Be(AnnualFeeErrors.NeedsReview);
        var estado = await EstadoAsync(db);
        estado.NextAttemptDate.Should().Be(Ahora.Date.AddDays(1));
        estado.CurrentAttemptIndex.Should().Be(0);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 3)]
    public async Task El_exito_registra_el_numero_de_intento_del_ciclo(int fallosPrevios, int esperado)
    {
        // Revisión M-5: misma convención que los fallos (1 = primer intento del ciclo).
        var (db, _) = await ConMiembroAsync();
        await using var __ = db;
        var e = await db.SubscriptionBillingStates.SingleAsync();
        e.CurrentAttemptIndex = fallosPrevios;
        e.Status = fallosPrevios > 0 ? RecurringBillingStatus.Retrying : RecurringBillingStatus.Active;
        await db.SaveChangesAsync();
        await CuotaAnualDb.TokenAsync(db, Miembro);

        await CobroAutomaticoDeCuotaAnualTests.Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        (await db.RecurringBillingAttempts.SingleAsync()).AttemptIndex.Should().Be(esperado);
    }

    [Fact]
    public async Task El_fallo_sin_fondos_registra_el_numero_de_intento_del_ciclo()
    {
        var (db, _) = await ConMiembroAsync(conSaldo: false);
        await using var __ = db;

        await CobroAutomaticoDeCuotaAnualTests.Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        (await db.RecurringBillingAttempts.SingleAsync()).AttemptIndex.Should().Be(1);
    }
}
