using FluentAssertions;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;

namespace MLMConquerorGlobalEdition.Domain.Tests;

public class TokenInstanceSignupValidatorTests
{
    private const string SponsorId   = "sponsor-001";
    private const string OtherMember = "other-002";
    private const string VipId       = "00000002-prod-0000-0000-000000000002";
    private const string EliteId     = "00000003-prod-0000-0000-000000000003";
    private const string TurboId     = "00000004-prod-0000-0000-000000000004";
    private const string SubId       = "00000005-prod-0000-0000-000000000005";

    private static readonly DateTime Now = new(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc);

    private static TokenType EnrollmentType(int id = 13) =>
        new() { Id = id, Name = "Enrollment: VIP Member", Category = TokenCategory.Enrollment, IsActive = true };

    private static TokenTransaction Instance(string ownerId, TokenInstanceStatus status, DateTime? expires = null) =>
        new()
        {
            Id                    = 1,
            MemberId              = ownerId,
            TokenTypeId           = 13,
            ReferenceId           = "X4P2A9N",
            Status                = status,
            OriginalOwnerMemberId = ownerId,
            ExpiresAt             = expires,
            CreatedBy             = "admin",
            CreationDate          = Now.AddDays(-1)
        };

    private static IReadOnlyCollection<TokenTypeProduct> GrantedLinks(params string[] productIds) =>
        productIds.Select(p => new TokenTypeProduct
        {
            TokenTypeId = 13, ProductId = p, Role = TokenProductRole.Granted
        }).ToList();

    [Fact]
    public void Validate_WhenIssuedTokenOwnedBySponsorAndProductsMatch_Succeeds()
    {
        var inst    = Instance(SponsorId, TokenInstanceStatus.Issued);
        var type    = EnrollmentType();
        var granted = GrantedLinks(VipId);

        var act = () => TokenInstanceSignupValidator.Validate(
            inst, type, granted, new[] { VipId }, Now);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WhenDistributedTokenOwnedBySponsor_Succeeds()
    {
        var inst    = Instance(SponsorId, TokenInstanceStatus.Distributed);
        var type    = EnrollmentType();
        var granted = GrantedLinks(VipId);

        var act = () => TokenInstanceSignupValidator.Validate(
            inst, type, granted, new[] { VipId }, Now);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WhenSelectedProductsAreSubsetOfGranted_Succeeds()
    {
        // Token grants Subscription + Elite. User selects only Elite (still a subset).
        var inst    = Instance(SponsorId, TokenInstanceStatus.Issued);
        var type    = EnrollmentType();
        var granted = GrantedLinks(SubId, EliteId);

        var act = () => TokenInstanceSignupValidator.Validate(
            inst, type, granted, new[] { EliteId }, Now);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WhenStatusIsUsed_ThrowsTokenAlreadyUsedExceptionWithGenericCode()
    {
        var inst = Instance(SponsorId, TokenInstanceStatus.Used);

        var act = () => TokenInstanceSignupValidator.Validate(
            inst, EnrollmentType(), GrantedLinks(VipId), new[] { VipId }, Now);

        act.Should().Throw<TokenAlreadyUsedException>()
           .Which.Code.Should().Be("TOKEN_NOT_VALID");
    }

    [Fact]
    public void Validate_WhenStatusIsVoided_ThrowsTokenAlreadyUsedException()
    {
        var inst = Instance(SponsorId, TokenInstanceStatus.Voided);

        var act = () => TokenInstanceSignupValidator.Validate(
            inst, EnrollmentType(), GrantedLinks(VipId), new[] { VipId }, Now);

        act.Should().Throw<TokenAlreadyUsedException>();
    }

    [Fact]
    public void Validate_WhenExpiresAtIsInThePast_ThrowsTokenExpiredException()
    {
        var inst = Instance(SponsorId, TokenInstanceStatus.Issued, expires: Now.AddDays(-1));

        var act = () => TokenInstanceSignupValidator.Validate(
            inst, EnrollmentType(), GrantedLinks(VipId), new[] { VipId }, Now);

        act.Should().Throw<TokenExpiredException>()
           .Which.Code.Should().Be("TOKEN_NOT_VALID");
    }

    [Fact]
    public void Validate_WhenCurrentOwnerIsNotSponsor_Succeeds()
    {
        // Token ownership is no longer tied to the sponsor: a code shared with
        // someone in the owner's downline must still redeem for that signup.
        var inst = Instance(OtherMember, TokenInstanceStatus.Issued);

        var act = () => TokenInstanceSignupValidator.Validate(
            inst, EnrollmentType(), GrantedLinks(VipId), new[] { VipId }, Now);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WhenSelectedProductsContainProductOutsideGrantedSet_ThrowsTokenProductMismatchException()
    {
        // Token grants only VIP. User selects Elite → mismatch.
        var inst    = Instance(SponsorId, TokenInstanceStatus.Issued);
        var granted = GrantedLinks(VipId);

        var act = () => TokenInstanceSignupValidator.Validate(
            inst, EnrollmentType(), granted, new[] { EliteId }, Now);

        var ex = act.Should().Throw<TokenProductMismatchException>().Which;
        ex.Code.Should().Be("TOKEN_PRODUCT_MISMATCH");
        ex.Message.Should().Contain(VipId);
    }

    [Fact]
    public void Validate_WhenSelectedProductsAreSupersetOfGranted_ThrowsTokenProductMismatchException()
    {
        // Token grants only VIP. User selects VIP + Turbo → not a subset.
        var inst    = Instance(SponsorId, TokenInstanceStatus.Issued);
        var granted = GrantedLinks(VipId);

        var act = () => TokenInstanceSignupValidator.Validate(
            inst, EnrollmentType(), granted, new[] { VipId, TurboId }, Now);

        act.Should().Throw<TokenProductMismatchException>();
    }

    [Fact]
    public void Validate_WhenSelectedProductsIsEmpty_ThrowsTokenProductMismatchException()
    {
        var inst = Instance(SponsorId, TokenInstanceStatus.Issued);

        var act = () => TokenInstanceSignupValidator.Validate(
            inst, EnrollmentType(), GrantedLinks(VipId), Array.Empty<string>(), Now);

        act.Should().Throw<TokenProductMismatchException>();
    }

    [Fact]
    public void Validate_WhenTokenTypeIsUpgradeCategory_ThrowsTokenAlreadyUsedException()
    {
        // Upgrade tokens are not legal in a NEW signup flow.
        var upgradeType = new TokenType
        {
            Id = 56,
            Name = "Upgrade: Guest to VIP",
            Category = TokenCategory.Upgrade,
            IsActive = true
        };
        var inst = Instance(SponsorId, TokenInstanceStatus.Issued);
        inst.TokenTypeId = 56;

        var act = () => TokenInstanceSignupValidator.Validate(
            inst, upgradeType, GrantedLinks(VipId), new[] { VipId }, Now);

        act.Should().Throw<TokenAlreadyUsedException>()
           .Which.Code.Should().Be("TOKEN_NOT_VALID");
    }

    [Fact]
    public void Validate_WhenTokenTypeIsInactive_ThrowsTokenAlreadyUsedException()
    {
        var type = EnrollmentType();
        type.IsActive = false;
        var inst = Instance(SponsorId, TokenInstanceStatus.Issued);

        var act = () => TokenInstanceSignupValidator.Validate(
            inst, type, GrantedLinks(VipId), new[] { VipId }, Now);

        act.Should().Throw<TokenAlreadyUsedException>();
    }

    [Fact]
    public void Validate_WhenInstanceHasNullReferenceId_ThrowsTokenAlreadyUsedException()
    {
        // Pure ledger rows (without ReferenceId) cannot be redeemed.
        var inst = Instance(SponsorId, TokenInstanceStatus.Issued);
        inst.ReferenceId = null;

        var act = () => TokenInstanceSignupValidator.Validate(
            inst, EnrollmentType(), GrantedLinks(VipId), new[] { VipId }, Now);

        act.Should().Throw<TokenAlreadyUsedException>();
    }
}
