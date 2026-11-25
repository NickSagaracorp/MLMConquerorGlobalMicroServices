using MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.GetAdminCommissionsGrid;
using MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.GetCommissionStats;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Commissions;

/// <summary>
/// El grid de comisiones del admin filtra, ordena y pagina en el servidor sobre TODA la tabla. Antes la pantalla
/// cargaba una página y el filtro de Syncfusion solo veía esas filas: una comisión que no estuviera en la primera
/// página no aparecía al filtrar por su beneficiario.
/// </summary>
public class GridDeComisionesTests
{
    private static readonly DateTime Base = new(2026, 1, 1);

    /// <summary>
    /// 150 comisiones de AMB-001 con fechas crecientes (la más reciente es la 149) y, la MÁS ANTIGUA de todas, una de
    /// AMB-XYZ: con lo más reciente primero, queda en la última página.
    /// </summary>
    private static async Task<AppDbContext> ConComisiones()
    {
        var db = InMemoryDbHelper.Create();
        db.CommissionTypes.Add(new CommissionType
        {
            Id = 1, Name = "Fast Start", IsActive = true, LevelNo = 1, PaymentDelayDays = 7, CreatedBy = "seed"
        });

        for (var i = 0; i < 150; i++)
            db.CommissionEarnings.Add(Comision($"C{i:000}", "AMB-001", Base.AddDays(i),
                i % 3 == 0 ? CommissionEarningStatus.Paid : CommissionEarningStatus.Pending));

        db.CommissionEarnings.Add(Comision("VIEJA", "AMB-XYZ", Base.AddYears(-1), CommissionEarningStatus.Cancelled));
        await db.SaveChangesAsync();
        return db;
    }

    private static CommissionEarning Comision(string id, string beneficiario, DateTime fecha, CommissionEarningStatus estado) => new()
    {
        Id = id, BeneficiaryMemberId = beneficiario, CommissionTypeId = 1, Amount = 10m, Status = estado,
        EarnedDate = fecha, CreationDate = fecha, CreatedBy = "seed"
    };

    private static GridDataRequest Pagina(int tamano = 50, params GridFilter[] filtros) => new()
    {
        Page = 1, PageSize = tamano, Filters = filtros.ToList()
    };

    [Fact]
    public async Task El_filtro_de_columna_busca_en_toda_la_tabla_no_solo_en_la_primera_pagina()
    {
        await using var db = await ConComisiones();
        var handler = new GetAdminCommissionsGridHandler(db);

        var r = await handler.Handle(new GetAdminCommissionsGridQuery(
            Pagina(50, new GridFilter { Field = "BeneficiaryMemberId", Operator = "equal", Value = "AMB-XYZ" })),
            CancellationToken.None);

        r.Value!.TotalCount.Should().Be(1);
        r.Value.Items.Single().Id.Should().Be("VIEJA");
        r.Value.Items.Single().CommissionTypeName.Should().Be("Fast Start");
    }

    [Fact]
    public async Task Sin_orden_lo_mas_reciente_va_primero()
    {
        await using var db = await ConComisiones();
        var handler = new GetAdminCommissionsGridHandler(db);

        var r = await handler.Handle(new GetAdminCommissionsGridQuery(Pagina()), CancellationToken.None);

        r.Value!.TotalCount.Should().Be(151);
        r.Value.Items.First().Id.Should().Be("C149");
        r.Value.Items.Select(x => x.EarnedDate).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task El_orden_de_la_columna_manda_sobre_el_de_por_defecto()
    {
        await using var db = await ConComisiones();
        var handler = new GetAdminCommissionsGridHandler(db);
        var grid = Pagina();
        grid.Sorts = new List<GridSort> { new() { Field = "EarnedDate", Direction = "asc" } };

        var r = await handler.Handle(new GetAdminCommissionsGridQuery(grid), CancellationToken.None);

        r.Value!.Items.First().Id.Should().Be("VIEJA");
    }

    [Fact]
    public async Task El_desplegable_de_estado_filtra_toda_la_tabla()
    {
        await using var db = await ConComisiones();
        var handler = new GetAdminCommissionsGridHandler(db);

        var pagadas    = await handler.Handle(new GetAdminCommissionsGridQuery(Pagina(), "Paid"), CancellationToken.None);
        var canceladas = await handler.Handle(new GetAdminCommissionsGridQuery(Pagina(), "cancelled"), CancellationToken.None);

        pagadas.Value!.TotalCount.Should().Be(50);
        pagadas.Value.Items.Should().OnlyContain(x => x.Status == "Paid");
        canceladas.Value!.Items.Single().Id.Should().Be("VIEJA");
    }

    [Fact]
    public async Task Un_estado_del_desplegable_que_no_existe_no_filtra()
    {
        await using var db = await ConComisiones();
        var handler = new GetAdminCommissionsGridHandler(db);

        var r = await handler.Handle(new GetAdminCommissionsGridQuery(Pagina(), "Nope"), CancellationToken.None);

        r.Value!.TotalCount.Should().Be(151);
    }

    [Fact]
    public async Task El_filtro_igual_de_la_columna_Status_se_aplica_sobre_la_entidad()
    {
        await using var db = await ConComisiones();
        var handler = new GetAdminCommissionsGridHandler(db);

        var r = await handler.Handle(new GetAdminCommissionsGridQuery(
            Pagina(50, new GridFilter { Field = "Status", Operator = "equal", Value = "pending" })),
            CancellationToken.None);
        var nada = await handler.Handle(new GetAdminCommissionsGridQuery(
            Pagina(50, new GridFilter { Field = "Status", Operator = "equal", Value = "Pend" })),
            CancellationToken.None);

        r.Value!.TotalCount.Should().Be(100);
        nada.Value!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Un_or_entre_filtros_deja_el_estado_en_la_proyeccion_y_da_lo_mismo()
    {
        await using var db = await ConComisiones();
        var handler = new GetAdminCommissionsGridHandler(db);

        var r = await handler.Handle(new GetAdminCommissionsGridQuery(Pagina(50,
            new GridFilter { Field = "Status", Operator = "equal", Value = "Cancelled" },
            new GridFilter { Field = "BeneficiaryMemberId", Operator = "equal", Value = "NADIE", Logic = "or" })),
            CancellationToken.None);

        r.Value!.TotalCount.Should().Be(1);
    }

    [Theory]
    [InlineData(1000, 100)]
    [InlineData(100, 100)]
    [InlineData(25, 25)]
    public async Task El_tamano_de_pagina_no_pasa_de_100(int pedido, int esperado)
    {
        await using var db = await ConComisiones();
        var handler = new GetAdminCommissionsGridHandler(db);

        var r = await handler.Handle(new GetAdminCommissionsGridQuery(Pagina(pedido)), CancellationToken.None);

        r.Value!.PageSize.Should().Be(esperado);
        r.Value.Items.Should().HaveCount(esperado);
    }

    [Fact]
    public async Task Los_contadores_son_de_toda_la_tabla()
    {
        await using var db = await ConComisiones();
        var handler = new GetCommissionStatsHandler(db, new NoOpCacheService());

        var r = await handler.Handle(new GetCommissionStatsQuery(), CancellationToken.None);

        r.Value!.Total.Should().Be(151);
        r.Value.Paid.Should().Be(50);
        r.Value.Pending.Should().Be(100);
        r.Value.Cancelled.Should().Be(1);
    }
}
