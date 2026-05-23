using FluentValidation.TestHelper;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Validators;

namespace MLMConquerorGlobalEdition.Signups.Tests.Validators;

public class PlacementRequestValidatorTests
{
    private readonly PlacementRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenValid_Passes()
    {
        var r = new PlacementRequest { PlaceUnderMemberId = "AMB-000123", Side = "Left" };
        _validator.TestValidate(r).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenMemberIdMalformed_Fails()
    {
        var r = new PlacementRequest { PlaceUnderMemberId = "abc; DROP TABLE", Side = "Left" };
        _validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.PlaceUnderMemberId);
    }

    [Fact]
    public void Validate_WhenRootMember_Passes()
    {
        var r = new PlacementRequest { PlaceUnderMemberId = "ROOT001", Side = "Right" };
        _validator.TestValidate(r).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenSideInvalid_Fails()
    {
        var r = new PlacementRequest { PlaceUnderMemberId = "AMB-000001", Side = "Middle" };
        _validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.Side);
    }
}

public class ValidateReplicateSiteRequestValidatorTests
{
    private readonly ValidateReplicateSiteRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _validator.TestValidate(new ValidateReplicateSiteRequest { Slug = "alice-1" })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenSlugEmpty_Fails()
        => _validator.TestValidate(new ValidateReplicateSiteRequest { Slug = "" })
            .ShouldHaveValidationErrorFor(x => x.Slug);

    [Fact]
    public void Validate_WhenSlugUppercase_Fails()
        => _validator.TestValidate(new ValidateReplicateSiteRequest { Slug = "BadCase" })
            .ShouldHaveValidationErrorFor(x => x.Slug);

    [Fact]
    public void Validate_WhenSlugContainsHyphenAtEnd_Fails()
        => _validator.TestValidate(new ValidateReplicateSiteRequest { Slug = "bad-" })
            .ShouldHaveValidationErrorFor(x => x.Slug);
}

public class ValidateSponsorRequestValidatorTests
{
    private readonly ValidateSponsorRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenMemberId_Passes()
        => _validator.TestValidate(new ValidateSponsorRequest { SponsorMemberId = "AMB-001" })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenSlug_Passes()
        => _validator.TestValidate(new ValidateSponsorRequest { SponsorMemberId = "john-doe" })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenInjection_Fails()
        => _validator.TestValidate(new ValidateSponsorRequest { SponsorMemberId = "<script>" })
            .ShouldHaveValidationErrorFor(x => x.SponsorMemberId);

    [Fact]
    public void Validate_WhenEmpty_Fails()
        => _validator.TestValidate(new ValidateSponsorRequest { SponsorMemberId = "" })
            .ShouldHaveValidationErrorFor(x => x.SponsorMemberId);
}

public class ValidateTokenRequestValidatorTests
{
    private readonly ValidateTokenRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenValid_Passes()
    {
        var r = new ValidateTokenRequest
        {
            Code = "ABCD-1234",
            SponsorReplicateSite = "john-doe",
            SelectedProductIds = new(),
        };
        _validator.TestValidate(r).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenCodeLowercase_Fails()
    {
        var r = new ValidateTokenRequest
        {
            Code = "lower",
            SponsorReplicateSite = "john-doe",
            SelectedProductIds = new(),
        };
        _validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Fact]
    public void Validate_WhenProductIdsTooMany_Fails()
    {
        var r = new ValidateTokenRequest
        {
            Code = "ABCDEF",
            SponsorReplicateSite = "john-doe",
            SelectedProductIds = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid().ToString()).ToList(),
        };
        _validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.SelectedProductIds);
    }
}

public class MembershipChangeRequestValidatorTests
{
    private readonly MembershipChangeRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _validator.TestValidate(new MembershipChangeRequest { NewMembershipLevelId = 2, Reason = "Promotion" })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenLevelIdZero_Fails()
        => _validator.TestValidate(new MembershipChangeRequest { NewMembershipLevelId = 0 })
            .ShouldHaveValidationErrorFor(x => x.NewMembershipLevelId);

    [Fact]
    public void Validate_WhenReasonContainsInjection_Fails()
        => _validator.TestValidate(new MembershipChangeRequest { NewMembershipLevelId = 2, Reason = "DROP TABLE; --" })
            .ShouldHaveValidationErrorFor(x => x.Reason);
}

public class SelectProductsRequestValidatorTests
{
    private readonly SelectProductsRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenSingleValidGuid_Passes()
    {
        var r = new SelectProductsRequest { ProductIds = new() { Guid.NewGuid().ToString() } };
        _validator.TestValidate(r).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenSeedStyleId_Passes()
    {
        var r = new SelectProductsRequest { ProductIds = new() { "00000003-prod-0000-0000-000000000003" } };
        _validator.TestValidate(r).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenIdMalformed_Fails()
    {
        var r = new SelectProductsRequest { ProductIds = new() { "not-a-guid" } };
        _validator.TestValidate(r).ShouldHaveValidationErrorFor("ProductIds[0]");
    }

    [Fact]
    public void Validate_WhenTooManyProducts_Fails()
    {
        var r = new SelectProductsRequest
        {
            ProductIds = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid().ToString()).ToList()
        };
        _validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.ProductIds);
    }
}
