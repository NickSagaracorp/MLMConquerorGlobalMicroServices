using MLMConquerorGlobalEdition.AdminAPI.Features.Subscriptions.GetSubscriptionStats;
using MLMConquerorGlobalEdition.AdminAPI.Features.Subscriptions.GetSubscriptionsGrid;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Subscriptions;

/// <summary>
/// El grid de Subscriptions filtra, ordena y pagina en el servidor sobre TODAS las suscripciones. Antes la pagina
/// cargaba una sola pagina y el filtro de Syncfusion solo veia esas filas: una suscripcion de la pagina 3 no aparecia
/// al filtrar.
/// </summary>
public class GridDeSuscripcionesTests
{
    private static readonly DateTime Base = new(2026, 1, 1);

    /// <summary>
    /// 120 suscripciones: la i se creo el dia i (la 119 es la mas nueva). Las multiplos de 10 estan canceladas y en
    /// nivel Elite; las demas activas en nivel Basic.
    /// </summary>
    private static async Task<AppDbContext> ConSuscripciones()
    {
        var db = InMemoryDbHelper.Create();
        db.MembershipLevels.AddRange(
            new MembershipLevel { Id = 1, Name = "Basic", CreatedBy = "seed" },
            new MembershipLevel { Id = 2, Name = "Elite", CreatedBy = "seed" });

        for (var i = 0; i < 120; i++)
        {
            var cancelada = i % 10 == 0;
            db.MembershipSubscriptions.Add(new MembershipSubscription
            {
                Id                 = $"SUB-{i:D3}",
                MemberId           = $"AMB-{i:D3}",
                MembershipLevelId  = cancelada ? 2 : 1,
                SubscriptionStatus = cancelada ? MembershipStatus.Cancelled : MembershipStatus.Active,
                StartDate          = Base.AddDays(i),
                EndDate            = Base.AddDays(i + 30),
                IsAutoRenew        = !cancelada,
                CreationDate       = Base.AddDays(i),
                CreatedBy          = "seed"
            });
        }
        await db.SaveChangesAsync();

        // El AuditInterceptor pone CreationDate = ahora al insertar; se fija despues, en una segunda grabacion, para
        // que cada fila tenga su dia y el orden y el rango de fechas se puedan comprobar.
        foreach (var s in db.MembershipSubscriptions.Local)
            s.CreationDate = Base.AddDays(int.Parse(s.Id[^3..]));
        await db.SaveChangesAsync();
        return db;
    }

    private static GridDataRequest Pagina(int page = 1, int size = 50, params GridFilter[] filtros) =>
        new() { Page = page, PageSize = size, Filters = filtros.ToList() };

    [Fact]
    public async Task Sin_orden_salen_las_mas_nuevas_primero()
    {
        await using var db = await ConSuscripciones();
        var r = await new GetSubscriptionsGridHandler(db).Handle(new GetSubscriptionsGridQuery(Pagina()), default);

        r.Value!.TotalCount.Should().Be(120);
        r.Value.Items.First().Id.Should().Be("SUB-119");
        r.Value.Items.Select(s => s.CreationDate).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task El_filtro_de_columna_abarca_toda_la_tabla_y_no_solo_la_primera_pagina()
    {
        await using var db = await ConSuscripciones();

        // AMB-005 es de las mas antiguas: con el orden por defecto cae en la tercera pagina de 50.
        var r = await new GetSubscriptionsGridHandler(db).Handle(new GetSubscriptionsGridQuery(
            Pagina(filtros: new GridFilter { Field = "MemberId", Operator = "equal", Value = "AMB-005" })), default);

        r.Value!.TotalCount.Should().Be(1);
        r.Value.Items.Single().Id.Should().Be("SUB-005");
    }

    [Fact]
    public async Task El_filtro_por_nivel_proyectado_cuenta_todas_las_filas()
    {
        await using var db = await ConSuscripciones();
        var r = await new GetSubscriptionsGridHandler(db).Handle(new GetSubscriptionsGridQuery(
            Pagina(filtros: new GridFilter { Field = "MembershipLevelName", Operator = "equal", Value = "Elite" })), default);

        r.Value!.TotalCount.Should().Be(12);
        r.Value.Items.Should().OnlyContain(s => s.MembershipLevelName == "Elite");
    }

    [Fact]
    public async Task El_estado_de_la_pagina_filtra_sobre_la_tabla()
    {
        await using var db = await ConSuscripciones();
        var r = await new GetSubscriptionsGridHandler(db).Handle(
            new GetSubscriptionsGridQuery(Pagina(), "Cancelled"), default);

        r.Value!.TotalCount.Should().Be(12);
        r.Value.Items.Should().OnlyContain(s => s.Status == "Cancelled");
    }

    [Fact]
    public async Task Un_estado_que_no_existe_se_rechaza()
    {
        await using var db = await ConSuscripciones();
        var r = await new GetSubscriptionsGridHandler(db).Handle(
            new GetSubscriptionsGridQuery(Pagina(), "Bogus"), default);

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(GetSubscriptionsGridHandler.InvalidStatusCode);
    }

    [Fact]
    public async Task La_pagina_no_pasa_de_100_filas()
    {
        await using var db = await ConSuscripciones();
        var r = await new GetSubscriptionsGridHandler(db).Handle(new GetSubscriptionsGridQuery(Pagina(size: 1000)), default);

        r.Value!.PageSize.Should().Be(GetSubscriptionsGridHandler.MaxPageSize);
        r.Value.Items.Should().HaveCount(100);
        r.Value.TotalCount.Should().Be(120);
    }

    [Fact]
    public async Task El_orden_pedido_se_aplica_a_toda_la_tabla()
    {
        await using var db = await ConSuscripciones();
        var grid = Pagina();
        grid.Sorts = new List<GridSort> { new() { Field = "MemberId", Direction = "asc" } };

        var r = await new GetSubscriptionsGridHandler(db).Handle(new GetSubscriptionsGridQuery(grid), default);

        r.Value!.Items.First().MemberId.Should().Be("AMB-000");
    }

    [Fact]
    public async Task Los_contadores_cuentan_toda_la_tabla()
    {
        await using var db = await ConSuscripciones();
        var r = await new GetSubscriptionStatsHandler(db, new NoOpCacheService())
            .Handle(new GetSubscriptionStatsQuery(), default);

        r.Value!.Active.Should().Be(108);
        r.Value.Cancelled.Should().Be(12);
        r.Value.AutoRenew.Should().Be(108);
    }
}
