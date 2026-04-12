using MLMConquerorGlobalEdition.CommissionEngine.Features.GetCommissionCollection;
using MLMConquerorGlobalEdition.CommissionEngine.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.CommissionEngine.Tests.Features;

public class GetCommissionCollectionHandlerTests
{
    private static readonly DateTime SeedNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static CommissionCategory BuildCategory() => new()
    {
        Id = 1, Name = "General", IsActive = true,
        CreatedBy = "seed", CreationDate = SeedNow
    };

    private static CommissionType BuildCommissionType() => new()
    {
        Id = 1, Name = "DailyResidual", IsActive = true,
        CommissionCategoryId = 1, Percentage = 0,
        CreatedBy = "seed", CreationDate = SeedNow
    };

    private static CommissionEarning BuildEarning(DateTime periodDate, decimal amount = 100) => new()
    {
        BeneficiaryMemberId = "AMB-001",
        CommissionTypeId    = 1,
        Amount              = amount,
        Status              = CommissionEarningStatus.Pending,
        EarnedDate          = periodDate,
        PaymentDate         = periodDate.AddDays(7),
        PeriodDate          = periodDate,
        CreatedBy           = "seed",
        CreationDate        = periodDate,
        LastUpdateDate      = periodDate
    };

    private static async Task SeedLookups(MLMConquerorGlobalEdition.Repository.Context.AppDbContext db)
    {
        await db.CommissionCategories.AddAsync(BuildCategory());
        await db.CommissionTypes.AddAsync(BuildCommissionType());
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WhenCollectionIdInvalid_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();

        var handler = new GetCommissionCollectionHandler(db);
        var result  = await handler.Handle(
            new GetCommissionCollectionQuery("not-a-date", 1, 20), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_COLLECTION_ID");
    }

    [Fact]
    public async Task Handle_WhenExactDateFormat_ReturnsMatchingEarnings()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedLookups(db);
        var targetDate = new DateTime(2026, 3, 1);
        await db.CommissionEarnings.AddRangeAsync(
            BuildEarning(targetDate),
            BuildEarning(targetDate.AddDays(1)));  // different date, should not appear
        await db.SaveChangesAsync();

        var handler = new GetCommissionCollectionHandler(db);
        var result  = await handler.Handle(
            new GetCommissionCollectionQuery("2026-03-01", 1, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WhenMonthFormat_ReturnsAllEarningsInMonth()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedLookups(db);
        await db.CommissionEarnings.AddRangeAsync(
            BuildEarning(new DateTime(2026, 3, 1)),
            BuildEarning(new DateTime(2026, 3, 15)),
            BuildEarning(new DateTime(2026, 4, 1)));  // different month
        await db.SaveChangesAsync();

        var handler = new GetCommissionCollectionHandler(db);
        var result  = await handler.Handle(
            new GetCommissionCollectionQuery("2026-03", 1, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_PaginationReturnsCorrectPage()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedLookups(db);
        for (int i = 0; i < 5; i++)
            await db.CommissionEarnings.AddAsync(BuildEarning(new DateTime(2026, 3, 1)));
        await db.SaveChangesAsync();

        var handler = new GetCommissionCollectionHandler(db);
        var result  = await handler.Handle(
            new GetCommissionCollectionQuery("2026-03-01", Page: 1, PageSize: 3), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(3);
        result.Value.TotalCount.Should().Be(5);
        result.Value.TotalPages.Should().Be(2);
        result.Value.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ExcludesSoftDeletedEarnings()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedLookups(db);
        var earning = BuildEarning(new DateTime(2026, 3, 1));
        earning.IsDeleted = true;
        await db.CommissionEarnings.AddAsync(earning);
        await db.CommissionEarnings.AddAsync(BuildEarning(new DateTime(2026, 3, 1)));
        await db.SaveChangesAsync();

        var handler = new GetCommissionCollectionHandler(db);
        var result  = await handler.Handle(
            new GetCommissionCollectionQuery("2026-03", 1, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
    }
}
