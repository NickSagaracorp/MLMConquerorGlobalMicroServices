using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// "Pagar ahora" (Plan K, K14): el miembro elige token, comisiones o eWallet; nunca tarjeta. Misma clave
/// = mismo resultado (K10). Solo cuentas activas, sin KeepQualified y dentro de la ventana de 30 días. El
/// interruptor del cobro automático no le afecta. Revive un estado parado.
/// </summary>
public class PagarAhoraTests
{
    private const string Miembro = "AMB-1";

    private static async Task<AppDbContext> ConMiembroAsync(
        MemberAccountStatus estado = MemberAccountStatus.Active, bool mantener = false, DateTime? vence = null,
        RecurringBillingStatus estadoCobro = RecurringBillingStatus.Active, bool conEstado = true,
        MemberType tipo = MemberType.Ambassador, DateTime? alta = null)
    {
        var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, Miembro, alta: alta, estado: estado, mantener: mantener, tipo: tipo);
        if (conEstado) await CuotaAnualDb.EstadoAsync(db, Miembro, vence ?? new DateTime(2027, 6, 5), estadoCobro);
        return db;
    }

    private static Task<Result<AnnualFeePaymentResultDto>> Pagar(
        AppDbContext db, string metodo, string? clave = null, Mock<IEWalletFunds>? ewallet = null) =>
        CobroAutomaticoDeCuotaAnualTests.Servicio(db, encendido: false, ewallet: ewallet)
            .PayNowAsync(new AnnualFeePayNowInput(Miembro, "user-001",
                new PayAnnualFeeRequest { Method = metodo, IdempotencyKey = clave ?? Guid.NewGuid().ToString("N") }));

    [Fact]
    public async Task Con_un_token_paga_y_devuelve_el_codigo_y_el_siguiente_vencimiento()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.TokenAsync(db, Miembro, codigo: "CUOTA-9");

        var r = await Pagar(db, AnnualFeeMethods.Token);

        r.IsSuccess.Should().BeTrue();
        r.Value!.Method.Should().Be(AnnualFeeMethods.Token);
        r.Value.Status.Should().Be(AnnualFeePaymentStatuses.Completed);
        r.Value.Amount.Should().Be(0m);
        r.Value.TokenCode.Should().Be("CUOTA-9");
        r.Value.CycleDueDate.Should().Be(new DateTime(2027, 6, 5));
        r.Value.NextDueDate.Should().Be(new DateTime(2028, 6, 10)); // K4 revisado: pagado tarde, un año desde el pago
        (await db.AnnualFeePayments.SingleAsync()).Source.Should().Be(AnnualFeePaymentSource.PayNow);
    }

    [Fact]
    public async Task Con_comisiones_cobra_el_precio_de_la_cuota()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);

        var r = await Pagar(db, "commissionbalance");

        r.Value!.Method.Should().Be(AnnualFeeMethods.CommissionBalance);
        r.Value.Amount.Should().Be(99m);
        (await db.AnnualFeePayments.SingleAsync()).CreatedBy.Should().Be("user-001");
    }

    [Fact]
    public async Task Con_eWallet_cobra_en_dos_fases()
    {
        await using var db = await ConMiembroAsync();
        var ew = new Mock<IEWalletFunds>();
        ew.Setup(e => e.UserNameOfAsync(Miembro, It.IsAny<CancellationToken>())).ReturnsAsync("amb-1");
        ew.Setup(e => e.BalanceAsync("amb-1", It.IsAny<CancellationToken>())).ReturnsAsync(Result<decimal>.Success(500m));
        ew.Setup(e => e.DebitAsync("amb-1", 99m, 500m, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new EWalletDebitOutcome(EWalletDebitStatus.Completed, null, "ok"));

        var r = await Pagar(db, AnnualFeeMethods.EWallet, ewallet: ew);

        r.Value!.Method.Should().Be(AnnualFeeMethods.EWallet);
        r.Value.Status.Should().Be(AnnualFeePaymentStatuses.Completed);
    }

    [Fact]
    public async Task La_misma_clave_devuelve_el_mismo_pago_sin_cobrar_otra_vez()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 500m);

        var primero = await Pagar(db, AnnualFeeMethods.CommissionBalance, "clave-1");
        var segundo = await Pagar(db, AnnualFeeMethods.CommissionBalance, "clave-1");

        segundo.IsSuccess.Should().BeTrue();
        segundo.Value!.PaymentId.Should().Be(primero.Value!.PaymentId);
        (await db.Orders.CountAsync()).Should().Be(1);
        (await db.CommissionEarnings.CountAsync(e => e.Amount < 0)).Should().Be(1);
    }

    [Fact]
    public async Task La_misma_clave_con_otro_metodo_no_es_un_reintento()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 500m);
        await Pagar(db, AnnualFeeMethods.CommissionBalance, "clave-1");

        var r = await Pagar(db, AnnualFeeMethods.Token, "clave-1");

        r.ErrorCode.Should().Be(AnnualFeeErrors.IdempotencyKeyReused);
    }

    [Theory]
    [InlineData("CreditCard")]
    [InlineData("Crypto")]
    [InlineData("1")]
    [InlineData("")]
    public async Task Tarjeta_cripto_o_un_numero_no_son_metodos_de_la_cuota(string metodo)
    {
        await using var db = await ConMiembroAsync();

        (await Pagar(db, metodo)).ErrorCode.Should().Be(AnnualFeeErrors.MethodNotSupported);
        (await db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sin_clave_valida_no_hace_nada()
    {
        await using var db = await ConMiembroAsync();

        (await Pagar(db, AnnualFeeMethods.Token, "clave con espacios")).ErrorCode.Should().Be(AnnualFeeErrors.IdempotencyKeyRequired);
    }

    [Fact]
    public async Task Fuera_de_la_ventana_de_30_dias_no_vence_todavia()
    {
        await using var db = await ConMiembroAsync(vence: CuotaAnualDb.Ahora.Date.AddDays(31));
        await CuotaAnualDb.TokenAsync(db, Miembro);

        (await Pagar(db, AnnualFeeMethods.Token)).ErrorCode.Should().Be(AnnualFeeErrors.NotDue);
    }

    [Fact]
    public async Task Pagada_la_cuota_un_segundo_pago_con_otra_clave_no_vence()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 500m);
        await Pagar(db, AnnualFeeMethods.CommissionBalance);

        (await Pagar(db, AnnualFeeMethods.CommissionBalance)).ErrorCode.Should().Be(AnnualFeeErrors.NotDue);
        (await db.CommissionEarnings.CountAsync(e => e.Amount < 0)).Should().Be(1);
    }

    [Fact]
    public async Task Con_KeepQualified_no_hay_cuota()
    {
        await using var db = await ConMiembroAsync(mantener: true);
        (await Pagar(db, AnnualFeeMethods.Token)).ErrorCode.Should().Be(AnnualFeeErrors.Waived);
    }

    [Fact]
    public async Task Con_la_cuenta_suspendida_no_se_paga()
    {
        await using var db = await ConMiembroAsync(estado: MemberAccountStatus.Suspended);
        (await Pagar(db, AnnualFeeMethods.Token)).ErrorCode.Should().Be(AnnualFeeErrors.AccountNotActive);
    }

    [Fact]
    public async Task Un_cliente_no_tiene_cuota()
    {
        await using var db = await ConMiembroAsync(tipo: MemberType.ExternalMember, conEstado: false);
        (await Pagar(db, AnnualFeeMethods.Token)).ErrorCode.Should().Be(AnnualFeeErrors.NotApplicable);
    }

    [Fact]
    public async Task Sin_token_dice_que_no_hay_y_la_misma_clave_vale_despues_de_comprarlo()
    {
        await using var db = await ConMiembroAsync();

        (await Pagar(db, AnnualFeeMethods.Token, "clave-t")).ErrorCode.Should().Be(AnnualFeeErrors.NoFeeToken);
        (await db.AnnualFeePayments.CountAsync()).Should().Be(0);

        await CuotaAnualDb.TokenAsync(db, Miembro);
        (await Pagar(db, AnnualFeeMethods.Token, "clave-t")).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Sin_estado_lo_crea_y_cobra_si_el_aniversario_cae_dentro_de_la_ventana()
    {
        await using var db = await ConMiembroAsync(conEstado: false, alta: new DateTime(2026, 6, 20));
        await CuotaAnualDb.TokenAsync(db, Miembro);

        var r = await Pagar(db, AnnualFeeMethods.Token);

        r.IsSuccess.Should().BeTrue();
        r.Value!.CycleDueDate.Should().Be(new DateTime(2027, 6, 20));
        r.Value.NextDueDate.Should().Be(new DateTime(2028, 6, 20));
    }

    [Fact]
    public async Task Revive_un_estado_parado_y_re_ancla_si_paga_muy_tarde()
    {
        await using var db = await ConMiembroAsync(vence: new DateTime(2027, 1, 5), estadoCobro: RecurringBillingStatus.Stopped);
        await CuotaAnualDb.ComisionAsync(db, Miembro, 500m);

        var r = await Pagar(db, AnnualFeeMethods.CommissionBalance);

        r.IsSuccess.Should().BeTrue();
        var estado = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();
        estado.Status.Should().Be(RecurringBillingStatus.Active);
        estado.NextBillingDate.Should().Be(new DateTime(2028, 6, 10));
    }

    [Fact]
    public async Task Con_el_candado_ocupado_no_cobra()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 500m);

        var r = await CobroAutomaticoDeCuotaAnualTests.Servicio(db, candado: new BusyGate())
            .PayNowAsync(new AnnualFeePayNowInput(Miembro, "user-001",
                new PayAnnualFeeRequest { Method = AnnualFeeMethods.CommissionBalance, IdempotencyKey = "k1" }));

        r.ErrorCode.Should().Be(AnnualFeeErrors.Busy);
        (await db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task HasPayment_dice_si_la_clave_ya_tiene_pago_de_este_miembro()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 500m);
        await Pagar(db, AnnualFeeMethods.CommissionBalance, "clave-1");
        var s = CobroAutomaticoDeCuotaAnualTests.Servicio(db);

        (await s.HasPaymentAsync(Miembro, "clave-1")).Should().BeTrue();
        (await s.HasPaymentAsync("AMB-2", "clave-1")).Should().BeFalse();
        (await s.HasPaymentAsync(Miembro, "otra")).Should().BeFalse();
        (await s.HasPaymentAsync(Miembro, null)).Should().BeFalse();
    }

    [Fact]
    public async Task Con_la_membresia_en_HoldByBilling_no_se_paga()
    {
        // Decisión de Nick (2026-10-17): con la membresía en HoldByBilling no hay cuota que cobrar.
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.TokenAsync(db, Miembro);
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            MemberId = Miembro, MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.HoldByBilling,
            StartDate = CuotaAnualDb.Ahora.AddDays(-100), CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();

        (await Pagar(db, AnnualFeeMethods.Token)).ErrorCode.Should().Be(AnnualFeeErrors.MembershipOnHold);
        (await db.AnnualFeePayments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_pago_adelantado_suma_un_anio_al_vencimiento_y_no_hace_perder_dias()
    {
        // K4 revisado por Nick: desde 30 días antes, el siguiente vence un año después del vencimiento actual.
        await using var db = await ConMiembroAsync(vence: new DateTime(2027, 7, 5));
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);

        var r = await Pagar(db, AnnualFeeMethods.CommissionBalance);

        r.Value!.CycleDueDate.Should().Be(new DateTime(2027, 7, 5));
        r.Value.NextDueDate.Should().Be(new DateTime(2028, 7, 5));
        (await db.SubscriptionBillingStates.AsNoTracking().SingleAsync()).BillingAnchorDate.Should().Be(new DateTime(2026, 6, 5));
    }

    [Fact]
    public async Task Quien_cancelo_la_membresia_puede_pagar_ahora_para_mantener_su_Biz_Center()
    {
        // Decisión de Nick (M-8): el cobro automático se pospone, pero Pagar ahora sigue disponible.
        await using var db = await ConMiembroAsync(estado: MemberAccountStatus.Inactive);
        await CuotaAnualDb.TokenAsync(db, Miembro);
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            MemberId = Miembro, MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.Cancelled,
            StartDate = CuotaAnualDb.Ahora.AddDays(-100), CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();

        (await Pagar(db, AnnualFeeMethods.Token)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task La_clave_de_un_pago_borrado_sigue_usada_como_en_el_indice_unico()
    {
        // Revisión M-6: UX_AnnualFeePayments_Member_IdempotencyKey incluye los borrados lógicos. La
        // idempotencia también los ve: si no, el reintento chocaría con el índice en SQL Server.
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 500m);
        db.AnnualFeePayments.Add(new MLMConquerorGlobalEdition.Domain.Entities.Billing.AnnualFeePayment
        {
            MemberId = Miembro, SubscriptionBillingStateId = "anual-" + Miembro, CycleDueDate = new DateTime(2026, 6, 5),
            Amount = 99m, Method = AnnualFeePaymentMethod.CommissionBalance, Source = AnnualFeePaymentSource.PayNow,
            Status = AnnualFeePaymentStatus.Completed, OrderId = "ord-borrado", IdempotencyKey = "clave-borrada",
            IsDeleted = true, CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();
        var s = CobroAutomaticoDeCuotaAnualTests.Servicio(db);

        (await s.HasPaymentAsync(Miembro, "clave-borrada")).Should().BeTrue();
        (await Pagar(db, AnnualFeeMethods.CommissionBalance, "clave-borrada")).ErrorCode.Should().Be(AnnualFeeErrors.IdempotencyKeyReused);
        (await db.CommissionEarnings.CountAsync(e => e.Amount < 0)).Should().Be(0);
    }
}
