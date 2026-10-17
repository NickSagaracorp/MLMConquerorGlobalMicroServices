using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// Revisión del Bloque 3 (C-1): una excepción dentro del candado que no es un choque de índice (un fallo
/// del consumidor de tokens, del débito o una DbUpdateConcurrencyException) deshace, suelta lo añadido y
/// relanza. El contexto lo comparte el trabajador: el siguiente SaveChanges no puede guardar un pedido o
/// un pago a medias.
/// </summary>
public class FallosBajoElCandadoDeLaCuotaTests
{
    private const string Miembro = "AMB-1";
    private static readonly DateTime Vence = new(2027, 6, 5);

    private static async Task<AppDbContext> ConMiembroAsync(SaveFaults? fallos = null)
    {
        var db = CuotaAnualDb.Nueva(interceptor: fallos);
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, Miembro);
        await CuotaAnualDb.EstadoAsync(db, Miembro, Vence);
        return db;
    }

    private static async Task NadaQuedoGuardadoAsync(AppDbContext db)
    {
        // El mismo contexto vuelve a guardar (como el trabajador al cerrar su trozo).
        await db.SaveChangesAsync();
        (await db.Orders.CountAsync()).Should().Be(0);
        (await db.AnnualFeePayments.CountAsync()).Should().Be(0);
        (await db.RecurringBillingAttempts.CountAsync()).Should().Be(0);
        var estado = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();
        estado.NextBillingDate.Should().Be(Vence);
        estado.LastSuccessfulBillingDate.Should().BeNull();
        db.ChangeTracker.Entries().Should().OnlyContain(e => e.State == EntityState.Unchanged);
    }

    [Fact]
    public async Task Si_el_consumidor_de_tokens_lanza_no_queda_nada_a_medias()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.TokenAsync(db, Miembro);
        var real = new AnnualFeeTokenConsumer(db);
        var tokens = new Mock<IAnnualFeeTokenConsumer>();
        tokens.Setup(t => t.FindAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
              .Returns((string m, DateTime n, CancellationToken c) => real.FindAsync(m, n, c));
        tokens.Setup(t => t.ConsumeAsync(It.IsAny<TokenTransaction>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("fallo al gastar el token"));

        var act = () => CobroAutomaticoDeCuotaAnualTests.Servicio(db, tokens: tokens.Object)
            .ChargeDueAsync("anual-" + Miembro, force: false);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await NadaQuedoGuardadoAsync(db);
    }

    [Fact]
    public async Task Una_DbUpdateConcurrencyException_al_confirmar_tampoco_deja_nada()
    {
        var fallos = new SaveFaults();
        await using var db = await ConMiembroAsync(fallos);
        await CuotaAnualDb.TokenAsync(db, Miembro);
        fallos.FailWhen(c => c.ChangeTracker.Entries<AnnualFeePayment>().Any(e => e.State == EntityState.Added), times: 1,
            make: _ => new DbUpdateConcurrencyException("otro proceso cambió el estado"));

        var act = () => CobroAutomaticoDeCuotaAnualTests.Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        await NadaQuedoGuardadoAsync(db);
        (await MLMConquerorGlobalEdition.Repository.Services.AnnualFee.AnnualFeeTokenQueries
            .Available(db, Miembro, CuotaAnualDb.Ahora).CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Si_el_debito_de_comisiones_lanza_no_queda_nada_a_medias()
    {
        await using var db = await ConMiembroAsync();
        var comisiones = new Mock<ICommissionBalanceService>();
        comisiones.Setup(c => c.GetSpendableAsync(Miembro, It.IsAny<CancellationToken>())).ReturnsAsync(500m);
        comisiones.Setup(c => c.FundAnnualFeeAsync(Miembro, It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ThrowsAsync(new TimeoutException("la base no contestó"));

        var act = () => CobroAutomaticoDeCuotaAnualTests.Servicio(db, comisiones: comisiones.Object)
            .ChargeDueAsync("anual-" + Miembro, force: false);

        await act.Should().ThrowAsync<TimeoutException>();
        await NadaQuedoGuardadoAsync(db);
    }

    [Fact]
    public async Task Pagar_ahora_con_un_fallo_dentro_del_candado_tampoco_deja_nada()
    {
        await using var db = await ConMiembroAsync();
        var comisiones = new Mock<ICommissionBalanceService>();
        comisiones.Setup(c => c.GetSpendableAsync(Miembro, It.IsAny<CancellationToken>())).ReturnsAsync(500m);
        comisiones.Setup(c => c.FundAnnualFeeAsync(Miembro, It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ThrowsAsync(new InvalidOperationException("fallo"));

        var act = () => CobroAutomaticoDeCuotaAnualTests.Servicio(db, comisiones: comisiones.Object)
            .PayNowAsync(new AnnualFeePayNowInput(Miembro, "user-1",
                new PayAnnualFeeRequest { Method = AnnualFeeMethods.CommissionBalance, IdempotencyKey = "clave-1" }));

        await act.Should().ThrowAsync<InvalidOperationException>();
        await NadaQuedoGuardadoAsync(db);
    }

    [Fact]
    public async Task Si_la_reserva_de_la_eWallet_lanza_no_queda_nada_a_medias()
    {
        var fallos = new SaveFaults();
        await using var db = await ConMiembroAsync(fallos);
        var ew = new Mock<MLMConquerorGlobalEdition.Billing.Services.Tokens.IEWalletFunds>();
        ew.Setup(e => e.UserNameOfAsync(Miembro, It.IsAny<CancellationToken>())).ReturnsAsync("amb-1");
        ew.Setup(e => e.BalanceAsync("amb-1", It.IsAny<CancellationToken>())).ReturnsAsync(Result<decimal>.Success(500m));
        fallos.FailWhen(c => c.ChangeTracker.Entries<AnnualFeePayment>().Any(e => e.State == EntityState.Added), times: 1,
            make: _ => new InvalidOperationException("la conexión se cayó"));

        var act = () => CobroAutomaticoDeCuotaAnualTests.Servicio(db, ewallet: ew).ChargeDueAsync("anual-" + Miembro, force: false);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await NadaQuedoGuardadoAsync(db);
    }

    [Fact]
    public async Task Si_la_conciliacion_lanza_al_guardar_no_queda_nada_a_medias()
    {
        var fallos = new SaveFaults();
        await using var db = await ConMiembroAsync(fallos);
        var pago = new AnnualFeePayment
        {
            MemberId = Miembro, SubscriptionBillingStateId = "anual-" + Miembro, CycleDueDate = Vence, Amount = 99m,
            Method = MLMConquerorGlobalEdition.Domain.Enums.AnnualFeePaymentMethod.EWallet,
            Source = MLMConquerorGlobalEdition.Domain.Enums.AnnualFeePaymentSource.Automatic,
            Status = MLMConquerorGlobalEdition.Domain.Enums.AnnualFeePaymentStatus.NeedsReconciliation, OrderId = "ord-1",
            CreatedBy = "prueba"
        };
        db.AnnualFeePayments.Add(pago);
        db.Orders.Add(new MLMConquerorGlobalEdition.Domain.Entities.Orders.Orders
        {
            Id = "ord-1", MemberId = Miembro, OrderNo = "AF-1", TotalAmount = 99m,
            Status = MLMConquerorGlobalEdition.Domain.Entities.Orders.OrderStatus.Processing, CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();
        fallos.FailWhen(c => c.ChangeTracker.Entries<MLMConquerorGlobalEdition.Domain.Entities.Orders.PaymentHistory>()
            .Any(e => e.State == EntityState.Added), times: 1, make: _ => new InvalidOperationException("caída"));

        var act = () => CobroAutomaticoDeCuotaAnualTests.Servicio(db).ReconcileAsync(pago.Id,
            new ReconcileAnnualFeePaymentRequest { Charged = true, GatewayReference = "IPY-1" }, "tesoreria");

        await act.Should().ThrowAsync<InvalidOperationException>();
        await db.SaveChangesAsync();
        (await db.PaymentHistories.CountAsync()).Should().Be(0);
        (await db.AnnualFeePayments.AsNoTracking().SingleAsync()).Status
            .Should().Be(MLMConquerorGlobalEdition.Domain.Enums.AnnualFeePaymentStatus.NeedsReconciliation);
        (await db.SubscriptionBillingStates.AsNoTracking().SingleAsync()).NextBillingDate.Should().Be(Vence);
    }
}
