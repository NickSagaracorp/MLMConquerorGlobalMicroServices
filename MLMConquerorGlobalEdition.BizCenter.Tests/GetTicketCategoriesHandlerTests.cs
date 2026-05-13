using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Features.Tickets.GetTicketCategories;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// Validates that the BizCenter "list categories" endpoint feeds the CreateTicketModal
/// with the right shape: only active categories, ordered by SortOrder then Name, and
/// projected to the Id/Name pair the UI binds against.
/// </summary>
public class GetTicketCategoriesHandlerTests : IDisposable
{
    private readonly AppDbContext _db;

    public GetTicketCategoriesHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private TicketCategory SeedCategory(int id, string name, bool isActive = true, int sortOrder = 0)
    {
        var c = new TicketCategory
        {
            Id           = id,
            Name         = name,
            IsActive     = isActive,
            SortOrder    = sortOrder,
            CreatedBy    = "seed",
            CreationDate = DateTime.UtcNow
        };
        _db.TicketCategories.Add(c);
        return c;
    }

    [Fact]
    public async Task Handle_WhenActiveCategoriesExist_ReturnsThemOrderedBySortThenName()
    {
        SeedCategory(1, "Billing",    sortOrder: 2);
        SeedCategory(2, "Account",    sortOrder: 2); // same sortOrder, alphabetic tiebreaker
        SeedCategory(3, "Membership", sortOrder: 1);
        await _db.SaveChangesAsync();

        var handler = new GetTicketCategoriesHandler(_db);
        var result  = await handler.Handle(new GetTicketCategoriesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var ids = result.Value!.Select(c => c.Id).ToArray();
        ids.Should().Equal(3, 2, 1); // Membership(1), Account(2,A), Billing(2,B)
    }

    [Fact]
    public async Task Handle_FiltersOutInactiveCategories()
    {
        SeedCategory(1, "Active");
        SeedCategory(2, "Hidden", isActive: false);
        await _db.SaveChangesAsync();

        var handler = new GetTicketCategoriesHandler(_db);
        var result  = await handler.Handle(new GetTicketCategoriesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Select(c => c.Name).Should().BeEquivalentTo(new[] { "Active" });
    }

    [Fact]
    public async Task Handle_WhenNoActiveCategoriesExist_ReturnsEmptyButSuccess()
    {
        SeedCategory(1, "DeprecatedOnly", isActive: false);
        await _db.SaveChangesAsync();

        var handler = new GetTicketCategoriesHandler(_db);
        var result  = await handler.Handle(new GetTicketCategoriesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }
}
