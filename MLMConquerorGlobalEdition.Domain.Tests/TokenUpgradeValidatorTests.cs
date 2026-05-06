using FluentAssertions;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;

namespace MLMConquerorGlobalEdition.Domain.Tests;

public class TokenUpgradeValidatorTests
{
    private const string GuestProductId = "00000001-prod-0000-0000-000000000001";
    private const string VipProductId   = "00000002-prod-0000-0000-000000000002";
    private const string EliteProductId = "00000003-prod-0000-0000-000000000003";
    private const string TurboProductId = "00000004-prod-0000-0000-000000000004";

    private static TokenType UpgradeTokenType(int id = 56, string name = "Upgrade: Guest to VIP") =>
        new() { Id = id, Name = name, Category = TokenCategory.Upgrade, IsActive = true };

    [Fact]
    public void ValidateUpgradePath_WhenFromAndToMatch_Succeeds()
    {
        var token = UpgradeTokenType();
        var links = new[]
        {
            new TokenTypeProduct { TokenTypeId = token.Id, ProductId = GuestProductId, Role = TokenProductRole.UpgradeFrom },
            new TokenTypeProduct { TokenTypeId = token.Id, ProductId = VipProductId,   Role = TokenProductRole.UpgradeTo   }
        };

        var act = () => TokenTypeUpgradeValidator.ValidateUpgradePath(token, links, GuestProductId, VipProductId);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateUpgradePath_WhenMemberHoldsDifferentProduct_ThrowsInvalidUpgradeTokenPathException()
    {
        var token = UpgradeTokenType();
        var links = new[]
        {
            new TokenTypeProduct { TokenTypeId = token.Id, ProductId = GuestProductId, Role = TokenProductRole.UpgradeFrom },
            new TokenTypeProduct { TokenTypeId = token.Id, ProductId = VipProductId,   Role = TokenProductRole.UpgradeTo   }
        };

        // Member holds Elite, not Guest as the token expects
        var act = () => TokenTypeUpgradeValidator.ValidateUpgradePath(token, links, EliteProductId, VipProductId);

        act.Should().Throw<InvalidUpgradeTokenPathException>()
           .Which.Code.Should().Be("INVALID_UPGRADE_TOKEN_PATH");
    }

    [Fact]
    public void ValidateUpgradePath_WhenTargetProductDoesNotMatch_ThrowsInvalidUpgradeTokenPathException()
    {
        var token = UpgradeTokenType();
        var links = new[]
        {
            new TokenTypeProduct { TokenTypeId = token.Id, ProductId = GuestProductId, Role = TokenProductRole.UpgradeFrom },
            new TokenTypeProduct { TokenTypeId = token.Id, ProductId = VipProductId,   Role = TokenProductRole.UpgradeTo   }
        };

        // Member tries to upgrade to Elite but token only allows VIP
        var act = () => TokenTypeUpgradeValidator.ValidateUpgradePath(token, links, GuestProductId, EliteProductId);

        act.Should().Throw<InvalidUpgradeTokenPathException>();
    }

    [Fact]
    public void ValidateUpgradePath_WhenTokenIsNotUpgradeCategory_ThrowsInvalidUpgradeTokenPathException()
    {
        var enrollmentToken = new TokenType
        {
            Id = 5,
            Name = "Enrollment: Ambassador + Elite",
            Category = TokenCategory.Enrollment,
            IsActive = true
        };
        var links = Array.Empty<TokenTypeProduct>();

        var act = () => TokenTypeUpgradeValidator.ValidateUpgradePath(enrollmentToken, links, GuestProductId, EliteProductId);

        act.Should().Throw<InvalidUpgradeTokenPathException>()
           .WithMessage("*not an Upgrade token*");
    }

    [Fact]
    public void ValidateUpgradePath_WhenMissingUpgradeFromLink_ThrowsInvalidUpgradeTokenPathException()
    {
        var token = UpgradeTokenType();
        var links = new[]
        {
            new TokenTypeProduct { TokenTypeId = token.Id, ProductId = VipProductId, Role = TokenProductRole.UpgradeTo }
        };

        var act = () => TokenTypeUpgradeValidator.ValidateUpgradePath(token, links, GuestProductId, VipProductId);

        act.Should().Throw<InvalidUpgradeTokenPathException>()
           .WithMessage("*exactly one UpgradeFrom and one UpgradeTo*");
    }

    [Fact]
    public void ValidateUpgradePath_WhenMissingUpgradeToLink_ThrowsInvalidUpgradeTokenPathException()
    {
        var token = UpgradeTokenType();
        var links = new[]
        {
            new TokenTypeProduct { TokenTypeId = token.Id, ProductId = GuestProductId, Role = TokenProductRole.UpgradeFrom }
        };

        var act = () => TokenTypeUpgradeValidator.ValidateUpgradePath(token, links, GuestProductId, VipProductId);

        act.Should().Throw<InvalidUpgradeTokenPathException>();
    }

    [Fact]
    public void ValidateUpgradePath_WhenMultipleUpgradeFromLinks_ThrowsInvalidUpgradeTokenPathException()
    {
        var token = UpgradeTokenType();
        var links = new[]
        {
            new TokenTypeProduct { TokenTypeId = token.Id, ProductId = GuestProductId, Role = TokenProductRole.UpgradeFrom },
            new TokenTypeProduct { TokenTypeId = token.Id, ProductId = VipProductId,   Role = TokenProductRole.UpgradeFrom },
            new TokenTypeProduct { TokenTypeId = token.Id, ProductId = EliteProductId, Role = TokenProductRole.UpgradeTo   }
        };

        var act = () => TokenTypeUpgradeValidator.ValidateUpgradePath(token, links, GuestProductId, EliteProductId);

        act.Should().Throw<InvalidUpgradeTokenPathException>();
    }

    [Fact]
    public void ValidateUpgradePath_EliteToTurbo_Succeeds()
    {
        var token = UpgradeTokenType(id: 66, name: "Upgrade: Elite to Turbo");
        var links = new[]
        {
            new TokenTypeProduct { TokenTypeId = token.Id, ProductId = EliteProductId, Role = TokenProductRole.UpgradeFrom },
            new TokenTypeProduct { TokenTypeId = token.Id, ProductId = TurboProductId, Role = TokenProductRole.UpgradeTo   }
        };

        var act = () => TokenTypeUpgradeValidator.ValidateUpgradePath(token, links, EliteProductId, TurboProductId);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateUpgradePath_ProductIdComparison_IsCaseInsensitive()
    {
        var token = UpgradeTokenType();
        var links = new[]
        {
            new TokenTypeProduct { TokenTypeId = token.Id, ProductId = GuestProductId.ToUpperInvariant(), Role = TokenProductRole.UpgradeFrom },
            new TokenTypeProduct { TokenTypeId = token.Id, ProductId = VipProductId,                       Role = TokenProductRole.UpgradeTo   }
        };

        var act = () => TokenTypeUpgradeValidator.ValidateUpgradePath(token, links, GuestProductId, VipProductId);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateUpgradePath_NullToken_ThrowsArgumentNullException()
    {
        var act = () => TokenTypeUpgradeValidator.ValidateUpgradePath(
            tokenType: null!,
            productLinks: Array.Empty<TokenTypeProduct>(),
            memberCurrentProductId: GuestProductId,
            requestedTargetProductId: VipProductId);

        act.Should().Throw<ArgumentNullException>();
    }
}
