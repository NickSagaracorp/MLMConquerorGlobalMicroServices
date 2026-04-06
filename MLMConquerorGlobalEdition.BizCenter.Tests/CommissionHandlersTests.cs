using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetBoostBonusCommissions;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetDualResidualCommissions;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetFastStartBonusCommissions;
using MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetPresidentialBonusCommissions;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

public class CommissionHandlersTests : IDisposable
{
    private const string MemberId = "member-comm-001";

    // Category IDs as defined in each handler
    private const int FastStartBonusCategoryId    = 2;
    private const int DualTeamResidualCategoryId  = 3;
    private const int BoostBonusCategoryId        = 4;
    private const int PresidentialBonusCategoryId = 4;

    private readonly AppDbContext _db;
    private readonly Mock<ICurrentUserService> _currentUser;

    public CommissionHandlersTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _currentUser = new Mock<ICurrentUserService>();
        _currentUser.Setup(x => x.MemberId).Returns(MemberId);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ────────────────────────────────────────────────────────────────

    private CommissionCategory SeedCategory(int id, string name)
    {
        var cat = new CommissionCategory { Id = id, Name = name };
        _db.CommissionCategories.Add(cat);
        return cat;
    }

    private CommissionType SeedType(int id, int categoryId, string name)
    {
        var type = new CommissionType { Id = id, CommissionCategoryId = categoryId, Name = name };
        _db.CommissionTypes.Add(type);
        return type;
    }

    private CommissionEarning SeedEarning(string beneficiaryId, int typeId, decimal amount,
        CommissionEarningStatus status = CommissionEarningStatus.Paid)
    {
        var e = new CommissionEarning
        {
            BeneficiaryMemberId = beneficiaryId,
            CommissionTypeId    = typeId,
            Amount              = amount,
            Status              = status,
            EarnedDate          = DateTime.UtcNow,
            PaymentDate         = DateTime.UtcNow
        };
        _db.CommissionEarnings.Add(e);
        return e;
    }

    // ── GetFastStartBonusCommissions ───────────────────────────────────────────

    [Fact]
    public async Task FastStartBonus_WhenMemberHasFsbEarnings_ReturnsThem()
    {
        SeedCategory(FastStartBonusCategoryId, "Fast Start Bonus");
        var type = SeedType(1, FastStartBonusCategoryId, "FSB Tier 1");
        SeedEarning(MemberId, type.Id, 200m);
        await _db.SaveChangesAsync();

        var handler = new GetFastStartBonusCommissionsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetFastStartBonusCommissionsQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items.First().Amount.Should().Be(200m);
    }

    [Fact]
    public async Task FastStartBonus_WhenNoFsbEarnings_ReturnsEmptyPage()
    {
        var handler = new GetFastStartBonusCommissionsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetFastStartBonusCommissionsQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(0);
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task FastStartBonus_WhenOtherMemberHasEarnings_DoesNotIncludeThem()
    {
        SeedCategory(FastStartBonusCategoryId, "Fast Start Bonus");
        var type = SeedType(1, FastStartBonusCategoryId, "FSB Tier 1");
        SeedEarning("other-member", type.Id, 500m);
        await _db.SaveChangesAsync();

        var handler = new GetFastStartBonusCommissionsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetFastStartBonusCommissionsQuery(1, 20), default);

        result.Value!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task FastStartBonus_WhenEarningsExceedPageSize_PaginatesCorrectly()
    {
        SeedCategory(FastStartBonusCategoryId, "FSB");
        var type = SeedType(1, FastStartBonusCategoryId, "FSB");
        for (int i = 0; i < 5; i++) SeedEarning(MemberId, type.Id, 100m * (i + 1));
        await _db.SaveChangesAsync();

        var handler = new GetFastStartBonusCommissionsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetFastStartBonusCommissionsQuery(1, 3), default);

        result.Value!.TotalCount.Should().Be(5);
        result.Value.Items.Should().HaveCount(3);
    }

    // ── GetDualResidualCommissions ─────────────────────────────────────────────

    [Fact]
    public async Task DualResidual_WhenMemberHasResidualEarnings_ReturnsThem()
    {
        SeedCategory(DualTeamResidualCategoryId, "Dual Team Residual");
        var type = SeedType(2, DualTeamResidualCategoryId, "Daily Residual");
        SeedEarning(MemberId, type.Id, 75m);
        await _db.SaveChangesAsync();

        var handler = new GetDualResidualCommissionsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetDualResidualCommissionsQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items.First().Amount.Should().Be(75m);
    }

    [Fact]
    public async Task DualResidual_WhenNoEarnings_ReturnsEmptyPage()
    {
        var handler = new GetDualResidualCommissionsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetDualResidualCommissionsQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task DualResidual_WhenFsbEarningsExist_DoesNotIncludeThem()
    {
        SeedCategory(FastStartBonusCategoryId, "Fast Start Bonus");
        var fsbType = SeedType(1, FastStartBonusCategoryId, "FSB");
        SeedEarning(MemberId, fsbType.Id, 300m);
        await _db.SaveChangesAsync();

        var handler = new GetDualResidualCommissionsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetDualResidualCommissionsQuery(1, 20), default);

        result.Value!.TotalCount.Should().Be(0);
    }

    // ── GetBoostBonusCommissions ───────────────────────────────────────────────

    [Fact]
    public async Task BoostBonus_WhenMemberHasBoostEarnings_ReturnsThem()
    {
        SeedCategory(BoostBonusCategoryId, "Boost Bonus");
        var type = SeedType(3, BoostBonusCategoryId, "Gold Boost Bonus");
        SeedEarning(MemberId, type.Id, 500m);
        await _db.SaveChangesAsync();

        var handler = new GetBoostBonusCommissionsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetBoostBonusCommissionsQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items.First().Amount.Should().Be(500m);
    }

    [Fact]
    public async Task BoostBonus_WhenTypeNameDoesNotContainBoost_IsExcluded()
    {
        // Same category ID (4) but name doesn't contain "Boost"
        SeedCategory(BoostBonusCategoryId, "Bonus Category");
        var type = SeedType(3, BoostBonusCategoryId, "Presidential Bonus");
        SeedEarning(MemberId, type.Id, 1000m);
        await _db.SaveChangesAsync();

        var handler = new GetBoostBonusCommissionsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetBoostBonusCommissionsQuery(1, 20), default);

        result.Value!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task BoostBonus_WhenNoBoostEarnings_ReturnsEmptyPage()
    {
        var handler = new GetBoostBonusCommissionsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetBoostBonusCommissionsQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(0);
    }

    // ── GetPresidentialBonusCommissions ───────────────────────────────────────

    [Fact]
    public async Task PresidentialBonus_WhenMemberHasPresidentialEarnings_ReturnsThem()
    {
        SeedCategory(PresidentialBonusCategoryId, "Presidential Bonus");
        var type = SeedType(4, PresidentialBonusCategoryId, "Presidential Bonus Tier 1");
        SeedEarning(MemberId, type.Id, 2000m);
        await _db.SaveChangesAsync();

        var handler = new GetPresidentialBonusCommissionsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetPresidentialBonusCommissionsQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items.First().Amount.Should().Be(2000m);
    }

    [Fact]
    public async Task PresidentialBonus_WhenTypeNameDoesNotContainPresidential_IsExcluded()
    {
        SeedCategory(PresidentialBonusCategoryId, "Bonus Category");
        var type = SeedType(4, PresidentialBonusCategoryId, "Gold Boost Bonus");
        SeedEarning(MemberId, type.Id, 500m);
        await _db.SaveChangesAsync();

        var handler = new GetPresidentialBonusCommissionsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetPresidentialBonusCommissionsQuery(1, 20), default);

        result.Value!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task PresidentialBonus_WhenNoEarnings_ReturnsEmptyPage()
    {
        var handler = new GetPresidentialBonusCommissionsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetPresidentialBonusCommissionsQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(0);
    }
}
