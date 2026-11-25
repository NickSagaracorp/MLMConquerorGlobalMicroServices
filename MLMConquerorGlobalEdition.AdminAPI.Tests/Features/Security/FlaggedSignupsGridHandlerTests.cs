using MLMConquerorGlobalEdition.AdminAPI.Features.Security.GetFlaggedSignupsGrid;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Security;

/// <summary>
/// El grid de "Flagged Signups" filtra, ordena y pagina en el servidor sobre TODOS los eventos que dejan pasar los
/// filtros de la cabecera: antes la página traía 25 y el filtro de la columna solo veía esos.
/// </summary>
public class FlaggedSignupsGridHandlerTests
{
    private static readonly DateTime Base = new(2026, 11, 1, 9, 0, 0);

    /// <summary>
    /// 150 eventos, uno por minuto: visitante v-i, IP 10.0.0.(i % 3), marcados los pares, liberados los múltiplos de 7.
    /// </summary>
    private static async Task<AppDbContext> SeedAsync(int count = 150)
    {
        var db = InMemoryDbHelper.Create();
        for (var i = 0; i < count; i++)
        {
            db.SignupRiskFingerprints.Add(new SignupRiskFingerprint
            {
                VisitorId    = $"v-{i:D4}",
                Flow         = SignupRiskFlow.AmbassadorSignup,
                IpAddress    = $"10.0.0.{i % 3}",
                IsFlagged    = i % 2 == 0,
                Cleared      = i % 7 == 0,
                CreationDate = Base.AddMinutes(i),
                CreatedBy    = "anon"
            });
        }
        await db.SaveChangesAsync();

        // AuditInterceptor pone CreationDate = ahora al insertar: las fechas se fijan en una segunda escritura.
        foreach (var f in db.SignupRiskFingerprints.Local) f.CreationDate = Base.AddMinutes(int.Parse(f.VisitorId[2..]));
        await db.SaveChangesAsync();
        return db;
    }

    private static GridDataRequest Grid(int pageSize = 50, params GridFilter[] filters) =>
        new() { Page = 1, PageSize = pageSize, Filters = filters.ToList() };

    [Fact]
    public async Task ColumnFilter_SpansTheWholeTable()
    {
        await using var db = await SeedAsync();
        var handler = new GetFlaggedSignupsGridHandler(db);

        // v-0001 es de los más antiguos: con el orden por defecto cae en la última página.
        var result = await handler.Handle(new GetFlaggedSignupsGridQuery(
            Grid(25, new GridFilter { Field = "VisitorId", Operator = "equal", Value = "v-0001" })), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Should().ContainSingle().Which.VisitorId.Should().Be("v-0001");
    }

    [Fact]
    public async Task NoSort_DefaultsToNewestFirst_AndHidesClearedByDefault()
    {
        await using var db = await SeedAsync();
        var handler = new GetFlaggedSignupsGridHandler(db);

        var result = await handler.Handle(new GetFlaggedSignupsGridQuery(Grid()), default);

        // 150 eventos menos los 22 liberados (múltiplos de 7 entre 0 y 147).
        result.Value!.TotalCount.Should().Be(128);
        result.Value.Items.First().VisitorId.Should().Be("v-0149");
        result.Value.Items.Select(x => x.CreationDate).Should().BeInDescendingOrder();
        result.Value.Items.Should().OnlyContain(x => !x.Cleared);
    }

    [Fact]
    public async Task PageLevelFilters_ComposeWithColumnFilters()
    {
        await using var db = await SeedAsync();
        var handler = new GetFlaggedSignupsGridHandler(db);

        // IP 10.0.0.0 = múltiplos de 3 (50); solo marcados = múltiplos de 6 (25); con liberados incluidos.
        var result = await handler.Handle(new GetFlaggedSignupsGridQuery(
            Grid(), IpAddress: "10.0.0.0", OnlyFlagged: true, IncludeCleared: true), default);
        result.Value!.TotalCount.Should().Be(25);

        // Y además un filtro de columna: los liberados de ese conjunto (múltiplos de 42: 0, 42, 84, 126).
        var cleared = await handler.Handle(new GetFlaggedSignupsGridQuery(
            Grid(50, new GridFilter { Field = "Cleared", Operator = "equal", Value = "true" }),
            IpAddress: "10.0.0.0", OnlyFlagged: true, IncludeCleared: true), default);
        cleared.Value!.TotalCount.Should().Be(4);
    }

    [Fact]
    public async Task DateRange_IsAppliedBeforeTheGrid()
    {
        await using var db = await SeedAsync();
        var handler = new GetFlaggedSignupsGridHandler(db);

        var result = await handler.Handle(new GetFlaggedSignupsGridQuery(
            Grid(), From: Base.AddMinutes(100), To: Base.AddMinutes(109), IncludeCleared: true), default);

        result.Value!.TotalCount.Should().Be(10);
    }

    [Fact]
    public async Task PageSize_IsCappedAt100()
    {
        await using var db = await SeedAsync();
        var handler = new GetFlaggedSignupsGridHandler(db);

        var result = await handler.Handle(new GetFlaggedSignupsGridQuery(Grid(pageSize: 200), IncludeCleared: true), default);

        result.Value!.PageSize.Should().Be(100);
        result.Value.Items.Should().HaveCount(100);
    }
}
