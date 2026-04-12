using MLMConquerorGlobalEdition.CommissionEngine.Features.GetCommissionRules;
using MLMConquerorGlobalEdition.CommissionEngine.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;

namespace MLMConquerorGlobalEdition.CommissionEngine.Tests.Features;

public class GetCommissionRulesHandlerTests
{
    private static readonly DateTime FixedNow = DateTime.UtcNow;

    private static CommissionCategory BuildCategory(int id = 1) => new()
    {
        Id           = id,
        Name         = "General",
        IsActive     = true,
        CreatedBy    = "seed",
        CreationDate = FixedNow
    };

    private static CommissionType BuildType(int id, string name, bool isActive = true,
        int categoryId = 1) => new()
    {
        Id                    = id,
        Name                  = name,
        IsActive              = isActive,
        CommissionCategoryId  = categoryId,
        Percentage            = 10,
        CreatedBy             = "seed",
        CreationDate          = FixedNow
    };

    [Fact]
    public async Task Handle_WhenNoTypes_ReturnsEmptyList()
    {
        await using var db = InMemoryDbHelper.Create();

        var handler = new GetCommissionRulesHandler(db);
        var result  = await handler.Handle(new GetCommissionRulesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyActiveTypes()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CommissionCategories.AddAsync(BuildCategory(1));
        await db.CommissionTypes.AddRangeAsync(
            BuildType(1, "FastStart",  isActive: true,  categoryId: 1),
            BuildType(2, "Deprecated", isActive: false, categoryId: 1));
        await db.SaveChangesAsync();

        var handler = new GetCommissionRulesHandler(db);
        var result  = await handler.Handle(new GetCommissionRulesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1);
        result.Value.Single().Name.Should().Be("FastStart");
    }

    [Fact]
    public async Task Handle_ReturnsAllActiveTypes()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CommissionCategories.AddAsync(BuildCategory(1));
        await db.CommissionTypes.AddRangeAsync(
            BuildType(1, "FastStart",    categoryId: 1),
            BuildType(2, "DailyResidual", categoryId: 1),
            BuildType(3, "BoostBonus",   categoryId: 1));
        await db.SaveChangesAsync();

        var handler = new GetCommissionRulesHandler(db);
        var result  = await handler.Handle(new GetCommissionRulesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(3);
    }
}
