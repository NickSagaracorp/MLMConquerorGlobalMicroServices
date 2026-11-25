using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payments;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payments.GetAdminPaymentsGrid;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Payments;

/// <summary>
/// El grid de Payment History filtra, ordena y pagina en el servidor sobre TODOS los pagos, y el rango de fechas y el
/// estado de la cabecera llegan al servidor (antes el rango se ignoraba y el filtro de Syncfusion solo veia la pagina
/// cargada).
/// </summary>
public class GridDePagosTests
{
    private static readonly DateTime Base = new(2026, 1, 1, 10, 0, 0);

    /// <summary>
    /// 150 pagos: el i se creo el dia i (el 149 es el mas nuevo). Los multiplos de 15 fallaron con PayQuicker; los
    /// demas se cobraron con Stripe.
    /// </summary>
    private static async Task<AppDbContext> ConPagos()
    {
        var db = InMemoryDbHelper.Create();
        for (var i = 0; i < 150; i++)
        {
            var fallido = i % 15 == 0;
            db.PaymentHistories.Add(new PaymentHistory
            {
                Id                   = $"PH-{i:D3}",
                MemberId             = $"AMB-{i:D3}",
                OrderId              = $"ORD-{i:D3}",
                Amount               = 10m + i,
                GatewayName          = fallido ? "PayQuicker" : "Stripe",
                GatewayTransactionId = $"TX-{i:D3}",
                TransactionStatus    = fallido ? PaymentHistoryTransactionStatus.Failed : PaymentHistoryTransactionStatus.Captured,
                FailureReason        = fallido ? "Declined" : null,
                CreationDate         = Base.AddDays(i),
                CreatedBy            = "seed"
            });
        }
        await db.SaveChangesAsync();

        // El AuditInterceptor pone CreationDate = ahora al insertar; se fija despues, en una segunda grabacion, para
        // que cada fila tenga su dia y el orden y el rango de fechas se puedan comprobar.
        foreach (var p in db.PaymentHistories.Local)
            p.CreationDate = Base.AddDays(int.Parse(p.Id[^3..]));
        await db.SaveChangesAsync();
        return db;
    }

    private static GridDataRequest Pagina(int size = 50, params GridFilter[] filtros) =>
        new() { Page = 1, PageSize = size, Filters = filtros.ToList() };

    private static Task<Result<PagedResult<AdminPaymentDto>>>
        Leer(AppDbContext db, GetAdminPaymentsGridQuery q) => new GetAdminPaymentsGridHandler(db).Handle(q, default);

    [Fact]
    public async Task Sin_orden_salen_los_mas_nuevos_primero()
    {
        await using var db = await ConPagos();
        var r = await Leer(db, new GetAdminPaymentsGridQuery(Pagina()));

        r.Value!.TotalCount.Should().Be(150);
        r.Value.Items.First().Id.Should().Be("PH-149");
        r.Value.Items.Select(p => p.CreationDate).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task El_filtro_de_columna_abarca_toda_la_tabla_y_no_solo_la_primera_pagina()
    {
        await using var db = await ConPagos();

        // Los pagos de PayQuicker estan repartidos por las tres paginas; deben salir los 10.
        var r = await Leer(db, new GetAdminPaymentsGridQuery(
            Pagina(filtros: new GridFilter { Field = "GatewayName", Operator = "equal", Value = "PayQuicker" })));

        r.Value!.TotalCount.Should().Be(10);
        r.Value.Items.Should().Contain(p => p.Id == "PH-000");
    }

    [Fact]
    public async Task El_filtro_numerico_por_importe_va_al_servidor()
    {
        await using var db = await ConPagos();
        var r = await Leer(db, new GetAdminPaymentsGridQuery(
            Pagina(filtros: new GridFilter { Field = "Amount", Operator = "lessthan", Value = "15" })));

        r.Value!.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task El_estado_de_la_pagina_filtra_sobre_la_tabla()
    {
        await using var db = await ConPagos();
        var r = await Leer(db, new GetAdminPaymentsGridQuery(Pagina(), StatusFilter: "Failed"));

        r.Value!.TotalCount.Should().Be(10);
        r.Value.Items.Should().OnlyContain(p => p.Status == "Failed");
    }

    [Fact]
    public async Task Un_estado_que_no_existe_se_rechaza()
    {
        await using var db = await ConPagos();
        var r = await Leer(db, new GetAdminPaymentsGridQuery(Pagina(), StatusFilter: "Completed"));

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(GetAdminPaymentsGridHandler.InvalidStatusCode);
    }

    [Fact]
    public async Task El_rango_de_fechas_incluye_el_dia_final_entero()
    {
        await using var db = await ConPagos();

        // Del dia 10 al 12: los pagos 10, 11 y 12 (el 12 es a las 10:00 del dia final).
        var r = await Leer(db, new GetAdminPaymentsGridQuery(Pagina(),
            From: Base.Date.AddDays(10), To: Base.Date.AddDays(12)));

        r.Value!.TotalCount.Should().Be(3);
        r.Value.Items.Select(p => p.Id).Should().BeEquivalentTo("PH-010", "PH-011", "PH-012");
    }

    [Fact]
    public async Task La_pagina_no_pasa_de_100_filas()
    {
        await using var db = await ConPagos();
        var r = await Leer(db, new GetAdminPaymentsGridQuery(Pagina(size: 1000)));

        r.Value!.PageSize.Should().Be(GetAdminPaymentsGridHandler.MaxPageSize);
        r.Value.Items.Should().HaveCount(100);
        r.Value.TotalCount.Should().Be(150);
    }
}
