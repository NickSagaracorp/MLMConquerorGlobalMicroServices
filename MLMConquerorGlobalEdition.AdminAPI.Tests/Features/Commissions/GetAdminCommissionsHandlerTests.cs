using MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.GetAdminCommissions;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Commissions;

public class GetAdminCommissionsHandlerTests
{
    private static PagedRequest Page(int page = 1, int size = 10) => new() { Page = page, PageSize = size };

    private static async Task SeedCommissionType(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db, int id = 1)
    {
        await db.CommissionTypes.AddAsync(new CommissionType
        {
            Id = id, Name = "Fast Start", IsActive = true, IsSponsorBonus = false,
            LevelNo = 1, PaymentDelayDays = 7, CreatedBy = "seed"
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WhenNoCommissions_ReturnsEmptyPagedResult()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new GetAdminCommissionsHandler(db);

        var result = await handler.Handle(new GetAdminCommissionsQuery(Page()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenCommissionsExist_ReturnsMappedDtos()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedCommissionType(db, 1);

        await db.CommissionEarnings.AddAsync(new CommissionEarning
        {
            BeneficiaryMemberId = "AMB-001",
            CommissionTypeId = 1,
            Amount = 150m,
            Status = CommissionEarningStatus.Pending,
            EarnedDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            IsManualEntry = false,
            CreationDate = DateTime.UtcNow,
            CreatedBy = "system"
        });
        await db.SaveChangesAsync();

        var handler = new GetAdminCommissionsHandler(db);

        var result = await handler.Handle(new GetAdminCommissionsQuery(Page()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        var dto = result.Value.Items.Single();
        dto.BeneficiaryMemberId.Should().Be("AMB-001");
        dto.Amount.Should().Be(150m);
        dto.Status.Should().Be("Pending");
        dto.IsManualEntry.Should().BeFalse();
        dto.CommissionTypeName.Should().Be("Fast Start");
    }

    [Fact]
    public async Task Handle_ReturnsPaginatedSubset()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedCommissionType(db, 1);

        for (int i = 0; i < 5; i++)
        {
            await db.CommissionEarnings.AddAsync(new CommissionEarning
            {
                BeneficiaryMemberId = $"AMB-{i:D3}",
                CommissionTypeId = 1,
                Amount = 10m * (i + 1),
                Status = CommissionEarningStatus.Pending,
                EarnedDate = DateTime.UtcNow.AddDays(-i),
                CreationDate = DateTime.UtcNow,
                CreatedBy = "system"
            });
        }
        await db.SaveChangesAsync();

        var handler = new GetAdminCommissionsHandler(db);

        var result = await handler.Handle(new GetAdminCommissionsQuery(Page(page: 1, size: 2)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(5);
        result.Value.Items.Count().Should().Be(2);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task Handle_OrdersByEarnedDateDescending()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedCommissionType(db, 1);

        var older = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        await db.CommissionEarnings.AddRangeAsync(
            new CommissionEarning { BeneficiaryMemberId = "AMB-OLD", CommissionTypeId = 1, Amount = 10m, Status = CommissionEarningStatus.Pending, EarnedDate = older, CreationDate = DateTime.UtcNow, CreatedBy = "system" },
            new CommissionEarning { BeneficiaryMemberId = "AMB-NEW", CommissionTypeId = 1, Amount = 20m, Status = CommissionEarningStatus.Pending, EarnedDate = newer, CreationDate = DateTime.UtcNow, CreatedBy = "system" }
        );
        await db.SaveChangesAsync();

        var handler = new GetAdminCommissionsHandler(db);

        var result = await handler.Handle(new GetAdminCommissionsQuery(Page()), CancellationToken.None);

        result.Value!.Items.First().BeneficiaryMemberId.Should().Be("AMB-NEW");
        result.Value.Items.Last().BeneficiaryMemberId.Should().Be("AMB-OLD");
    }
}
