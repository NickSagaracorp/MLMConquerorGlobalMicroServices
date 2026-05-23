using FluentValidation.TestHelper;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Validators;

namespace MLMConquerorGlobalEdition.Signups.Tests.Validators;

public class MemberSignupRequestValidatorTests
{
    private readonly MemberSignupRequestValidator _validator = new();

    private static MemberSignupRequest ValidRequest() => new()
    {
        FirstName = "Bob",
        LastName = "Lee",
        Email = "bob@example.com",
        Password = "P@ssw0rd!",
        Country = "CA",
        MembershipLevelId = 2,
    };

    [Fact]
    public void Validate_WhenAllFieldsValid_PassesValidation()
        => _validator.TestValidate(ValidRequest()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenFirstNameEmpty_FailsValidation()
    {
        var r = ValidRequest(); r.FirstName = "";
        _validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void Validate_WhenPasswordWeak_FailsValidation()
    {
        var r = ValidRequest(); r.Password = "short";
        _validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_WhenCountryNotIso2_FailsValidation()
    {
        var r = ValidRequest(); r.Country = "USA";
        _validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.Country);
    }

    [Fact]
    public void Validate_WhenUsRequiresSsn_FailsValidation()
    {
        var r = ValidRequest(); r.Country = "US";
        _validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.Ssn);
    }

    [Fact]
    public void Validate_WhenCityContainsHtml_FailsValidation()
    {
        var r = ValidRequest(); r.City = "<b>Toronto</b>";
        _validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.City);
    }

    [Fact]
    public void Validate_WhenMembershipLevelZero_FailsValidation()
    {
        var r = ValidRequest(); r.MembershipLevelId = 0;
        _validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.MembershipLevelId);
    }

    [Fact]
    public void Validate_WhenEmailMissing_FailsValidation()
    {
        var r = ValidRequest(); r.Email = "";
        _validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.Email);
    }
}
