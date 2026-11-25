using MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.GetAdminTicketsGrid;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.TicketAdmin;

/// <summary>
/// El grid de tickets del admin filtra, ordena y pagina en el servidor sobre TODOS los tickets: antes la página traía
/// una sola página y el filtro de Syncfusion solo veía esas filas.
/// </summary>
public class AdminTicketsGridHandlerTests
{
    private static readonly DateTime Base = new(2026, 11, 1, 9, 0, 0);

    /// <summary>
    /// 150 tickets, uno por minuto: el ticket i es del miembro AMB-i, está abierto salvo los múltiplos de 10 (cerrados)
    /// y el número 7 es el único del miembro "AMB-SOLO", creado el primero de todos (queda en la última página).
    /// </summary>
    private static async Task<AppDbContext> SeedAsync(int count = 150)
    {
        var db = InMemoryDbHelper.Create();
        db.TicketCategories.Add(new TicketCategory { Id = 1, Name = "General", IsActive = true, CreatedBy = "seed" });
        for (var i = 0; i < count; i++)
        {
            db.Tickets.Add(new Ticket
            {
                Id           = $"T-{i:D4}",
                TicketNumber = $"HD-{i:D4}",
                MemberId     = i == 0 ? "AMB-SOLO" : $"AMB-{i:D4}",
                CategoryId   = 1,
                Subject      = $"Subject {i}",
                Body         = "Body",
                Status       = i % 10 == 0 ? TicketStatus.Closed : TicketStatus.Open,
                CreationDate = Base.AddMinutes(i),
                CreatedBy    = "seed"
            });
        }
        await db.SaveChangesAsync();

        // AuditInterceptor pone CreationDate = ahora al insertar: la fecha de cada ticket se fija en una segunda
        // escritura (al modificar no la toca).
        foreach (var t in db.Tickets.Local) t.CreationDate = Base.AddMinutes(int.Parse(t.Id[2..]));
        await db.SaveChangesAsync();
        return db;
    }

    private static GridDataRequest Grid(int pageSize = 50, params GridFilter[] filters) =>
        new() { Page = 1, PageSize = pageSize, Filters = filters.ToList() };

    [Fact]
    public async Task ColumnFilter_SpansTheWholeTable_NotJustTheFirstPage()
    {
        await using var db = await SeedAsync();
        var handler = new GetAdminTicketsGridHandler(db);

        // El ticket de AMB-SOLO es el más antiguo: con el orden por defecto cae fuera de la primera página.
        var result = await handler.Handle(new GetAdminTicketsGridQuery(
            Grid(50, new GridFilter { Field = "MemberId", Operator = "equal", Value = "AMB-SOLO" })), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Should().ContainSingle().Which.Id.Should().Be("T-0000");
    }

    [Fact]
    public async Task NoSort_DefaultsToNewestFirst()
    {
        await using var db = await SeedAsync();
        var handler = new GetAdminTicketsGridHandler(db);

        var result = await handler.Handle(new GetAdminTicketsGridQuery(Grid()), default);

        result.Value!.TotalCount.Should().Be(150);
        result.Value.Items.First().Id.Should().Be("T-0149");
        result.Value.Items.Select(t => t.CreationDate).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task StatusQueryParam_FiltersBeforeProjection()
    {
        await using var db = await SeedAsync();
        var handler = new GetAdminTicketsGridHandler(db);

        var result = await handler.Handle(new GetAdminTicketsGridQuery(Grid(), Status: "Closed"), default);

        result.Value!.TotalCount.Should().Be(15);
        result.Value.Items.Should().OnlyContain(t => t.Status == "Closed");
    }

    [Fact]
    public async Task UnknownStatus_IsIgnored()
    {
        await using var db = await SeedAsync(20);
        var handler = new GetAdminTicketsGridHandler(db);

        var result = await handler.Handle(new GetAdminTicketsGridQuery(Grid(), Status: "NotAStatus"), default);

        result.Value!.TotalCount.Should().Be(20);
    }

    [Fact]
    public async Task PageSize_IsCappedAt100()
    {
        await using var db = await SeedAsync();
        var handler = new GetAdminTicketsGridHandler(db);

        var result = await handler.Handle(new GetAdminTicketsGridQuery(Grid(pageSize: 1000)), default);

        result.Value!.PageSize.Should().Be(100);
        result.Value.Items.Should().HaveCount(100);
        result.Value.TotalCount.Should().Be(150);
    }

    [Fact]
    public async Task Body_IsNotSentToTheGrid()
    {
        await using var db = await SeedAsync(3);
        var handler = new GetAdminTicketsGridHandler(db);

        var result = await handler.Handle(new GetAdminTicketsGridQuery(Grid()), default);

        result.Value!.Items.Should().OnlyContain(t => t.Body == string.Empty);
    }
}
