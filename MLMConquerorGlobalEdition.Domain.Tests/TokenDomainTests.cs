using FluentAssertions;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;

namespace MLMConquerorGlobalEdition.Domain.Tests;

public class TokenDomainTests
{
    [Fact]
    public void TokenDistribution_WhenInsufficientBalance_ThrowsInsufficientTokenBalanceException()
    {
        var balance = new TokenBalance { MemberId = "m1", TokenTypeId = 1, Balance = 5 };

        var action = () => balance.Deduct(10);

        action.Should().Throw<InsufficientTokenBalanceException>()
            .Which.Code.Should().Be("INSUFFICIENT_TOKEN_BALANCE");
    }

    [Fact]
    public void TokenDistribution_WithSufficientBalance_DeductsCorrectly()
    {
        var balance = new TokenBalance { MemberId = "m1", TokenTypeId = 1, Balance = 10 };

        balance.Deduct(4);

        balance.Balance.Should().Be(6);
    }

    [Fact]
    public void GuestPassToken_WhenUsed_SetsUsedByAndUsedAt()
    {
        var usedBy = "member-42";
        var usedAt = new DateTime(2026, 3, 14, 10, 0, 0);

        var transaction = new TokenTransaction
        {
            MemberId = "m1",
            TokenTypeId = 1,
            TransactionType = TokenTransactionType.Used,
            Quantity = 1,
            UsedByMemberId = usedBy,
            UsedAt = usedAt
        };

        transaction.UsedByMemberId.Should().Be(usedBy);
        transaction.UsedAt.Should().Be(usedAt);
    }

    [Fact]
    public void TokenBalance_WhenDeductingExactBalance_BalanceBecomesZero()
    {
        var balance = new TokenBalance { MemberId = "m1", TokenTypeId = 1, Balance = 5 };

        balance.Deduct(5);

        balance.Balance.Should().Be(0);
    }

    [Fact]
    public void TokenBalance_WhenAddingTokens_BalanceIncreasesCorrectly()
    {
        var balance = new TokenBalance { MemberId = "m1", TokenTypeId = 1, Balance = 3 };

        balance.Add(7);

        balance.Balance.Should().Be(10);
    }

    [Fact]
    public void TokenDistribution_WhenBalanceIsZero_ThrowsInsufficientTokenBalanceException()
    {
        var balance = new TokenBalance { MemberId = "m1", TokenTypeId = 1, Balance = 0 };

        var action = () => balance.Deduct(1);

        action.Should().Throw<InsufficientTokenBalanceException>();
    }
}
