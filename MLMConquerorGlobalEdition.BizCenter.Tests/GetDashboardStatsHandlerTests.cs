using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Features.Dashboard.GetDashboardStats;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

public class GetDashboardStatsHandlerTests : IDisposable
{
    private const string MemberId = "member-001";

    private readonly AppDbContext _db;
    private readonly Mock<ICurrentUserService> _currentUser;
    private readonly Mock<IDateTimeProvider> _dateTime;

    public GetDashboardStatsHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _currentUser = new Mock<ICurrentUserService>();
        _currentUser.Setup(x => x.MemberId).Returns(MemberId);

        _dateTime = new Mock<IDateTimeProvider>();
        _dateTime.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);
    }

    public void Dispose() => _db.Dispose();

    private GetDashboardStatsHandler CreateHandler() =>
        new(_db, _currentUser.Object, _dateTime.Object, new RankComputationService(_db));


    [Fact]
    public async Task Handle_WhenMemberHasPaidCommissions_ReturnsSumOfPaidAmounts()
    {
        _db.CommissionEarnings.AddRange(
            new CommissionEarning { BeneficiaryMemberId = MemberId, Amount = 100m, Status = CommissionEarningStatus.Paid,   EarnedDate = DateTime.UtcNow, PaymentDate = DateTime.UtcNow },
            new CommissionEarning { BeneficiaryMemberId = MemberId, Amount = 250m, Status = CommissionEarningStatus.Paid,   EarnedDate = DateTime.UtcNow, PaymentDate = DateTime.UtcNow },
            new CommissionEarning { BeneficiaryMemberId = MemberId, Amount = 999m, Status = CommissionEarningStatus.Pending, EarnedDate = DateTime.UtcNow, PaymentDate = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        var result = await CreateHandler().Handle(new GetDashboardStatsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalEarnings.Should().Be(350m);
    }

    [Fact]
    public async Task Handle_WhenMemberHasNoCommissions_ReturnsZeroEarnings()
    {
        var result = await CreateHandler().Handle(new GetDashboardStatsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalEarnings.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_WhenOtherMemberHasPaidCommissions_DoesNotIncludeTheirEarnings()
    {
        _db.CommissionEarnings.Add(new CommissionEarning
        {
            BeneficiaryMemberId = "other-member",
            Amount  = 500m,
            Status  = CommissionEarningStatus.Paid,
            EarnedDate  = DateTime.UtcNow,
            PaymentDate = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var result = await CreateHandler().Handle(new GetDashboardStatsQuery(), default);

        result.Value!.TotalEarnings.Should().Be(0m);
    }


    [Fact]
    public async Task Handle_WhenMemberHasDirectChildren_ReturnsCorrectTeamSize()
    {
        _db.GenealogyTree.AddRange(
            new GenealogyEntity { MemberId = "child-1", ParentMemberId = MemberId, HierarchyPath = $"/{MemberId}/child-1/" },
            new GenealogyEntity { MemberId = "child-2", ParentMemberId = MemberId, HierarchyPath = $"/{MemberId}/child-2/" },
            new GenealogyEntity { MemberId = "grandchild", ParentMemberId = "child-1", HierarchyPath = $"/{MemberId}/child-1/grandchild/" }
        );
        await _db.SaveChangesAsync();

        var result = await CreateHandler().Handle(new GetDashboardStatsQuery(), default);

        result.Value!.TeamSize.Should().Be(2); // only direct children
    }

    [Fact]
    public async Task Handle_WhenMemberHasNoTeam_ReturnsZeroTeamSize()
    {
        var result = await CreateHandler().Handle(new GetDashboardStatsQuery(), default);

        result.Value!.TeamSize.Should().Be(0);
    }


    [Fact]
    public async Task Handle_WhenMemberHasMultipleTokenBalances_ReturnsSummedBalance()
    {
        _db.TokenBalances.AddRange(
            new TokenBalance { MemberId = MemberId, Balance = 10, TokenTypeId = 1 },
            new TokenBalance { MemberId = MemberId, Balance = 5,  TokenTypeId = 2 },
            new TokenBalance { MemberId = "other",  Balance = 99, TokenTypeId = 1 }
        );
        await _db.SaveChangesAsync();

        var result = await CreateHandler().Handle(new GetDashboardStatsQuery(), default);

        result.Value!.TokenBalance.Should().Be(15);
    }


    [Fact]
    public async Task Handle_WhenMemberHasFsbEarnings_ReturnsFsbWindowsUpToThree()
    {
        var fsbType = new CommissionType { Id = 1, Name = "Fast Start Bonus", CommissionCategoryId = 1 };
        _db.CommissionTypes.Add(fsbType);
        await _db.SaveChangesAsync();

        for (int i = 0; i < 4; i++)
        {
            _db.CommissionEarnings.Add(new CommissionEarning
            {
                BeneficiaryMemberId = MemberId,
                Amount              = 50m * (i + 1),
                Status              = CommissionEarningStatus.Paid,
                CommissionTypeId    = fsbType.Id,
                EarnedDate          = DateTime.UtcNow.AddDays(-i),
                PaymentDate         = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();

        var result = await CreateHandler().Handle(new GetDashboardStatsQuery(), default);

        result.Value!.FsbWindows.Should().HaveCount(3); // capped at 3
    }

    [Fact]
    public async Task Handle_WhenMemberHasNoFsbEarningsAndNoCountdown_ReturnsThreeLockedWindows()
    {
        var result = await CreateHandler().Handle(new GetDashboardStatsQuery(), default);

        // Dashboard always renders 3 windows; without a countdown record they are all "Locked".
        result.Value!.FsbWindows.Should().HaveCount(3);
        result.Value!.FsbWindows.Should().OnlyContain(w => w.Status == "Locked");
    }


    [Fact]
    public async Task Handle_Always_ReturnsSuccess()
    {
        var result = await CreateHandler().Handle(new GetDashboardStatsQuery(), default);

        result.IsSuccess.Should().BeTrue();
    }
}
