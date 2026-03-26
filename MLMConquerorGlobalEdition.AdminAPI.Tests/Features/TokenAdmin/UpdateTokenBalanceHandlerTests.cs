using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenAdmin;
using MLMConquerorGlobalEdition.AdminAPI.Features.TokenAdmin.UpdateTokenBalance;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.TokenAdmin;

public class UpdateTokenBalanceHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<ICurrentUserService> CurrentUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("admin-001");
        return m;
    }

    private static Mock<IDateTimeProvider> DateTimeProvider()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    [Fact]
    public async Task Handle_WhenTokenBalanceNotFound_ReturnsTokenBalanceNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UpdateTokenBalanceHandler(db, CurrentUser().Object, DateTimeProvider().Object);

        var result = await handler.Handle(
            new UpdateTokenBalanceCommand("nonexistent-id", new AdminUpdateTokenBalanceRequest { Balance = 10 }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_BALANCE_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenTokenBalanceExists_SetsBalanceDirectly()
    {
        await using var db = InMemoryDbHelper.Create();
        var balance = new TokenBalance
        {
            MemberId = "AMB-001",
            TokenTypeId = 1,
            Balance = 10,
            CreationDate = FixedNow.AddDays(-5),
            LastUpdateDate = FixedNow.AddDays(-5),
            CreatedBy = "seed"
        };
        await db.TokenBalances.AddAsync(balance);
        await db.SaveChangesAsync();

        var handler = new UpdateTokenBalanceHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        var result = await handler.Handle(
            new UpdateTokenBalanceCommand(balance.Id, new AdminUpdateTokenBalanceRequest { Balance = 99 }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Balance.Should().Be(99);

        db.TokenBalances.Single().Balance.Should().Be(99);
    }

    [Fact]
    public async Task Handle_WhenUpdated_SetsLastUpdateByToCurrentUser()
    {
        await using var db = InMemoryDbHelper.Create();
        var balance = new TokenBalance
        {
            MemberId = "AMB-001",
            TokenTypeId = 1,
            Balance = 5,
            CreationDate = FixedNow,
            LastUpdateDate = FixedNow,
            CreatedBy = "seed"
        };
        await db.TokenBalances.AddAsync(balance);
        await db.SaveChangesAsync();

        var handler = new UpdateTokenBalanceHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        await handler.Handle(
            new UpdateTokenBalanceCommand(balance.Id, new AdminUpdateTokenBalanceRequest { Balance = 20 }),
            CancellationToken.None);

        db.TokenBalances.Single().LastUpdateBy.Should().Be("admin-001");
    }
}
