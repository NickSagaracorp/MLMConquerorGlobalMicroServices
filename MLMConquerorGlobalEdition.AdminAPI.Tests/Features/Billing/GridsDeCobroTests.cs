using MediatR;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;
using IEncryptionService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IEncryptionService;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Billing;

/// <summary>
/// Los grids de servidor de cobros del admin (intentos recurrentes, estados recurrentes e intentos de cobro contra la
/// pasarela): el filtro de columna y el orden abarcan toda la tabla y no solo la primera página, sin orden pedido sale
/// lo más reciente primero, los controles de la página siguen filtrando y la página nunca pasa de 100 filas.
/// </summary>
public class GridsDeCobroTests
{
    private static readonly DateTime Base = new(2026, 11, 1, 8, 0, 0);

    private static AdminRecurringBillingController Recurrentes(AppDbContext db) =>
        new(db, new Mock<IMediator>().Object);

    private static AdminBillingGatewayController Pasarela(AppDbContext db) =>
        new(db, new NoOpCacheService(), new Mock<IEncryptionService>().Object);

    private static PagedResult<T> Pagina<T>(IActionResult r)
    {
        var ok = r.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<ApiResponse<PagedResult<T>>>().Subject;
        body.Success.Should().BeTrue();
        return body.Data!;
    }

    private static GridDataRequest Pedir(int pageSize = 50, int page = 1, params GridFilter[] filtros) =>
        new() { Page = page, PageSize = pageSize, Filters = filtros.ToList() };

    // ── Intentos recurrentes ────────────────────────────────────────────────

    /// <summary>150 intentos, uno por hora; el de índice 7 (de los más viejos) es del miembro "M-RARO".</summary>
    private static AppDbContext ConIntentos()
    {
        var db = InMemoryDbHelper.Create();
        for (var i = 0; i < 150; i++)
        {
            db.RecurringBillingAttempts.Add(new RecurringBillingAttempt
            {
                SubscriptionBillingStateId = $"S-{i}",
                MemberId      = i == 7 ? "M-RARO" : $"M-{i:000}",
                ProductId     = "P-1",
                AttemptIndex  = 0,
                AttemptedAt   = Base.AddHours(i),
                Amount        = 10 + i,
                FundingSource = i % 2 == 0 ? RecurringFundingSource.CreditCard : RecurringFundingSource.CommissionBalance,
                Outcome       = i % 3 == 0 ? RecurringAttemptOutcome.Failed : RecurringAttemptOutcome.Success,
            });
        }
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Intentos_el_filtro_de_columna_encuentra_filas_fuera_de_la_primera_pagina()
    {
        using var db = ConIntentos();

        var p = Pagina<AdminRecurringBillingController.RecurringAttemptDto>(await Recurrentes(db).GetAttemptsGrid(
            Pedir(filtros: new GridFilter { Field = "MemberId", Operator = "equal", Value = "M-RARO" })));

        p.TotalCount.Should().Be(1);
        p.Items.Single().AttemptedAt.Should().Be(Base.AddHours(7));
    }

    [Fact]
    public async Task Intentos_sin_orden_pedido_salen_del_mas_reciente_al_mas_viejo()
    {
        using var db = ConIntentos();

        var p = Pagina<AdminRecurringBillingController.RecurringAttemptDto>(
            await Recurrentes(db).GetAttemptsGrid(Pedir(pageSize: 25)));

        p.TotalCount.Should().Be(150);
        p.Items.First().AttemptedAt.Should().Be(Base.AddHours(149));
        p.Items.Select(a => a.AttemptedAt).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Intentos_el_orden_del_grid_abarca_toda_la_tabla()
    {
        using var db = ConIntentos();
        var pedido = Pedir(pageSize: 10);
        pedido.Sorts = new() { new GridSort { Field = "Amount", Direction = "asc" } };

        var p = Pagina<AdminRecurringBillingController.RecurringAttemptDto>(await Recurrentes(db).GetAttemptsGrid(pedido));

        p.Items.First().Amount.Should().Be(10m);
    }

    [Fact]
    public async Task Intentos_los_controles_de_la_pagina_filtran_antes_que_el_grid()
    {
        using var db = ConIntentos();

        var p = Pagina<AdminRecurringBillingController.RecurringAttemptDto>(await Recurrentes(db).GetAttemptsGrid(
            Pedir(pageSize: 100),
            outcome: RecurringAttemptOutcome.Failed,
            fundingSource: RecurringFundingSource.CreditCard,
            fromDate: Base.AddHours(30), toDate: Base.AddHours(60)));

        // Fallidos (múltiplos de 3) con tarjeta (pares) = múltiplos de 6 entre 30 y 60: 30, 36, 42, 48, 54, 60.
        p.TotalCount.Should().Be(6);
        p.Items.Should().OnlyContain(a => a.Outcome == RecurringAttemptOutcome.Failed
                                       && a.FundingSource == RecurringFundingSource.CreditCard);

        var porMiembro = Pagina<AdminRecurringBillingController.RecurringAttemptDto>(
            await Recurrentes(db).GetAttemptsGrid(Pedir(), memberId: " M-RARO "));
        porMiembro.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Intentos_la_pagina_no_pasa_de_100_filas()
    {
        using var db = ConIntentos();

        var p = Pagina<AdminRecurringBillingController.RecurringAttemptDto>(
            await Recurrentes(db).GetAttemptsGrid(Pedir(pageSize: 1000)));

        p.PageSize.Should().Be(100);
        p.Items.Should().HaveCount(100);
        p.TotalCount.Should().Be(150);
    }

    // ── Estados recurrentes ─────────────────────────────────────────────────

    /// <summary>
    /// 130 estados repartidos en dos planes. Los de índice par tienen último intento; los impares nunca se intentaron y
    /// ordenan por su fecha de alta. El 3 es el más reciente de todos por fecha de alta y nunca se intentó.
    /// </summary>
    private static AppDbContext ConEstados()
    {
        var db = InMemoryDbHelper.Create();
        db.RecurringBillingPlans.AddRange(
            new RecurringBillingPlan { Id = 1, Name = "Monthly Elite", RetryCadenceDays = "1,3" },
            new RecurringBillingPlan { Id = 2, Name = "Annual Fee",    RetryCadenceDays = "1,3" });
        for (var i = 0; i < 130; i++)
        {
            db.SubscriptionBillingStates.Add(new SubscriptionBillingState
            {
                Id                     = $"S-{i:000}",
                MemberId               = i == 9 ? "M-RARO" : $"M-{i:000}",
                RecurringBillingPlanId = i < 5 ? 2 : 1,
                BillingAnchorDate      = Base,
                NextBillingDate        = Base.AddMonths(1),
                NextAttemptDate        = Base.AddMonths(1),
                LastAttemptAt          = i % 2 == 0 ? Base.AddHours(i).AddMinutes(30) : null,
                Status                 = i % 4 == 0 ? RecurringBillingStatus.Retrying : RecurringBillingStatus.Active,
            });
        }
        db.SaveChanges();

        // AuditInterceptor pone la fecha de alta al insertar; al modificar la respeta, así que se fija después.
        foreach (var s in db.SubscriptionBillingStates)
        {
            var i = int.Parse(s.Id[2..]);
            s.CreationDate = i == 3 ? Base.AddDays(30) : Base.AddHours(i);
        }
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return db;
    }

    [Fact]
    public async Task Estados_el_filtro_de_columna_encuentra_filas_fuera_de_la_primera_pagina()
    {
        using var db = ConEstados();

        var p = Pagina<AdminRecurringBillingController.RecurringStateDto>(await Recurrentes(db).GetStatesGrid(
            Pedir(pageSize: 25, filtros: new GridFilter { Field = "PlanName", Operator = "contains", Value = "annual" })));

        p.TotalCount.Should().Be(5);
        p.Items.Should().OnlyContain(s => s.PlanName == "Annual Fee");
    }

    [Fact]
    public async Task Estados_sin_orden_pedido_sale_primero_el_ultimo_intento_o_la_ultima_alta()
    {
        using var db = ConEstados();

        var p = Pagina<AdminRecurringBillingController.RecurringStateDto>(
            await Recurrentes(db).GetStatesGrid(Pedir(pageSize: 25)));

        p.TotalCount.Should().Be(130);
        // El 3 nunca se intentó pero se dio de alta el último; después el 129 (alta a las 129 h, sin intento) y el 128
        // (intento a las 128 h 30 min): se compara el último intento o, si no lo hay, la fecha de alta.
        p.Items.Take(3).Select(s => s.Id).Should().Equal("S-003", "S-129", "S-128");
    }

    [Fact]
    public async Task Estados_los_controles_de_la_pagina_filtran_antes_que_el_grid()
    {
        using var db = ConEstados();

        var p = Pagina<AdminRecurringBillingController.RecurringStateDto>(await Recurrentes(db).GetStatesGrid(
            Pedir(pageSize: 100), status: RecurringBillingStatus.Retrying, planId: 1));

        // Retrying = múltiplos de 4 (0..128 → 33) menos los del plan 2 (0 y 4).
        p.TotalCount.Should().Be(31);
        p.Items.Should().OnlyContain(s => s.Status == RecurringBillingStatus.Retrying && s.RecurringBillingPlanId == 1);

        var porMiembro = Pagina<AdminRecurringBillingController.RecurringStateDto>(
            await Recurrentes(db).GetStatesGrid(Pedir(), memberId: "M-RARO"));
        porMiembro.Items.Single().Id.Should().Be("S-009");
    }

    [Fact]
    public async Task Estados_la_pagina_no_pasa_de_100_filas()
    {
        using var db = ConEstados();

        var p = Pagina<AdminRecurringBillingController.RecurringStateDto>(
            await Recurrentes(db).GetStatesGrid(Pedir(pageSize: 500)));

        p.PageSize.Should().Be(100);
        p.Items.Should().HaveCount(100);
    }

    // ── Intentos de cobro contra la pasarela ────────────────────────────────

    /// <summary>140 intentos, uno por minuto, rotando procesador; solo el 11 es de Shift4.</summary>
    private static AppDbContext ConIntentosDeCobro()
    {
        var db = InMemoryDbHelper.Create();
        for (var i = 0; i < 140; i++)
        {
            db.GatewayChargeAttempts.Add(new GatewayChargeAttempt
            {
                MemberId          = $"M-{i % 10}",
                CardProcessor     = i == 11 ? CardProcessor.Shift4 : (i % 2 == 0 ? CardProcessor.CheckoutUS : CardProcessor.NmiSpreedly),
                Outcome           = i % 5 == 0 ? "Failed" : "Success",
                OriginalAmountUsd = 100 + i,
                ConvertedAmount   = 100 + i,
                AttemptedAtUtc    = Base.AddMinutes(i),
            });
        }
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Cobros_el_filtro_de_columna_encuentra_filas_fuera_de_la_primera_pagina()
    {
        using var db = ConIntentosDeCobro();

        var p = Pagina<AdminBillingGatewayController.ChargeAttemptDto>(await Pasarela(db).GetChargeAttemptsGrid(
            Pedir(pageSize: 25, filtros: new GridFilter { Field = "ProcessorName", Operator = "equal", Value = "Shift4" })));

        p.TotalCount.Should().Be(1);
        p.Items.Single().AttemptedAtUtc.Should().Be(Base.AddMinutes(11));
    }

    [Fact]
    public async Task Cobros_sin_orden_pedido_salen_del_mas_reciente_al_mas_viejo()
    {
        using var db = ConIntentosDeCobro();

        var p = Pagina<AdminBillingGatewayController.ChargeAttemptDto>(
            await Pasarela(db).GetChargeAttemptsGrid(Pedir(pageSize: 25)));

        p.TotalCount.Should().Be(140);
        p.Items.First().AttemptedAtUtc.Should().Be(Base.AddMinutes(139));
        p.Items.Select(a => a.AttemptedAtUtc).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Cobros_el_procesador_de_la_pagina_filtra_toda_la_tabla_y_no_la_pagina_cargada()
    {
        using var db = ConIntentosDeCobro();

        var p = Pagina<AdminBillingGatewayController.ChargeAttemptDto>(await Pasarela(db).GetChargeAttemptsGrid(
            Pedir(pageSize: 100), outcome: "Failed", processor: CardProcessor.CheckoutUS));

        // Fallidos (múltiplos de 5) y pares → múltiplos de 10 entre 0 y 139: 14.
        p.TotalCount.Should().Be(14);
        p.Items.Should().OnlyContain(a => a.CardProcessor == CardProcessor.CheckoutUS && a.Outcome == "Failed");

        var shift4 = Pagina<AdminBillingGatewayController.ChargeAttemptDto>(
            await Pasarela(db).GetChargeAttemptsGrid(Pedir(), processor: CardProcessor.Shift4, memberId: "M-1"));
        shift4.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Cobros_la_pagina_no_pasa_de_100_filas()
    {
        using var db = ConIntentosDeCobro();

        var p = Pagina<AdminBillingGatewayController.ChargeAttemptDto>(
            await Pasarela(db).GetChargeAttemptsGrid(Pedir(pageSize: 250)));

        p.PageSize.Should().Be(100);
        p.Items.Should().HaveCount(100);
    }

    [Fact]
    public async Task Los_GET_de_siempre_siguen_devolviendo_la_misma_pagina()
    {
        using var db = ConIntentosDeCobro();

        var p = Pagina<AdminBillingGatewayController.ChargeAttemptDto>(
            await Pasarela(db).GetChargeAttempts(page: 1, pageSize: 20));

        p.TotalCount.Should().Be(140);
        p.Items.Should().HaveCount(20);
        p.Items.First().ProcessorName.Should().Be(nameof(CardProcessor.NmiSpreedly));
    }
}
