using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenAdmin;
using MLMConquerorGlobalEdition.AdminAPI.Features.TokenAdmin.GrantTokens;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.TokenAdmin;

public class GrantTokensHandlerTests
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

    private static async Task SeedTokenType(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db,
        int id, string name = "Guest Pass", bool isGuestPass = true)
    {
        await db.TokenTypes.AddAsync(new TokenType
        {
            Id = id,
            Name = name,
            IsGuestPass = isGuestPass,
            IsActive = true,
            CreatedBy = "seed"
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WhenTokenTypeNotFound_ReturnsTokenTypeNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new GrantTokensHandler(db, CurrentUser().Object, DateTimeProvider().Object);

        var result = await handler.Handle(
            new GrantTokensCommand(new AdminGrantTokenRequest
            {
                MemberId = "AMB-001",
                TokenTypeId = 999,
                Quantity = 5
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_TYPE_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenNoExistingBalance_CreatesNewBalanceAndAddsTokens()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedTokenType(db, 1);
        var handler = new GrantTokensHandler(db, CurrentUser().Object, DateTimeProvider().Object);

        var result = await handler.Handle(
            new GrantTokensCommand(new AdminGrantTokenRequest
            {
                MemberId = "AMB-001",
                TokenTypeId = 1,
                Quantity = 5
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Balance.Should().Be(5);

        db.TokenBalances.Single().Balance.Should().Be(5);
    }

    [Fact]
    public async Task Handle_WhenExistingBalance_AddsToExistingBalance()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedTokenType(db, 1);
        await db.TokenBalances.AddAsync(new TokenBalance
        {
            MemberId = "AMB-001",
            TokenTypeId = 1,
            Balance = 10,
            CreationDate = FixedNow.AddDays(-5),
            LastUpdateDate = FixedNow.AddDays(-5),
            CreatedBy = "seed"
        });
        await db.SaveChangesAsync();

        var handler = new GrantTokensHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        var result = await handler.Handle(
            new GrantTokensCommand(new AdminGrantTokenRequest
            {
                MemberId = "AMB-001",
                TokenTypeId = 1,
                Quantity = 3
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Balance.Should().Be(13);
    }

    [Fact]
    public async Task Handle_WhenGranted_CreatesAdminGrantedTransaction()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedTokenType(db, 1);
        var handler = new GrantTokensHandler(db, CurrentUser().Object, DateTimeProvider().Object);

        await handler.Handle(
            new GrantTokensCommand(new AdminGrantTokenRequest
            {
                MemberId = "AMB-001",
                TokenTypeId = 1,
                Quantity = 5,
                Notes = "Bonus grant"
            }),
            CancellationToken.None);

        // Handler creates one transaction per token (Quantity=1 each)
        var txs = db.TokenTransactions.ToList();
        txs.Should().HaveCount(5);
        txs.Should().AllSatisfy(tx =>
        {
            tx.TransactionType.Should().Be(TokenTransactionType.AdminGranted);
            tx.MemberId.Should().Be("AMB-001");
            tx.Quantity.Should().Be(1);
            tx.Notes.Should().Be("Bonus grant");
        });
    }
}
