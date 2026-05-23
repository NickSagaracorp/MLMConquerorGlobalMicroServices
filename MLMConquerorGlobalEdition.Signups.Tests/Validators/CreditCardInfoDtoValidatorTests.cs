using FluentValidation.TestHelper;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Validators;

namespace MLMConquerorGlobalEdition.Signups.Tests.Validators;

public class CreditCardInfoDtoValidatorTests
{
    private readonly CreditCardInfoDtoValidator _validator = new();

    private static CreditCardInfoDto Valid() => new()
    {
        GatewayToken = "nonce_abc123",
        CardToken    = "tok_def456",
        Last4        = "4242",
        First6       = "424242",
        CardBrand    = "Visa",
        ExpiryMonth  = 12,
        ExpiryYear   = DateTime.UtcNow.Year + 2,
        Gateway      = "stripe",
    };

    [Fact]
    public void Validate_WhenAllFieldsValid_Passes()
        => _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenGatewayTokenHasSpecialChars_Fails()
    {
        var c = Valid(); c.GatewayToken = "<script>";
        _validator.TestValidate(c).ShouldHaveValidationErrorFor(x => x.GatewayToken);
    }

    [Fact]
    public void Validate_WhenLast4NotFourDigits_Fails()
    {
        var c = Valid(); c.Last4 = "42";
        _validator.TestValidate(c).ShouldHaveValidationErrorFor(x => x.Last4);
    }

    [Fact]
    public void Validate_WhenFirst6NotSixDigits_Fails()
    {
        var c = Valid(); c.First6 = "abc";
        _validator.TestValidate(c).ShouldHaveValidationErrorFor(x => x.First6);
    }

    [Fact]
    public void Validate_WhenExpiryMonthOutOfRange_Fails()
    {
        var c = Valid(); c.ExpiryMonth = 13;
        _validator.TestValidate(c).ShouldHaveValidationErrorFor(x => x.ExpiryMonth);
    }

    [Fact]
    public void Validate_WhenExpiryYearTooFarOut_Fails()
    {
        var c = Valid(); c.ExpiryYear = DateTime.UtcNow.Year + 50;
        _validator.TestValidate(c).ShouldHaveValidationErrorFor(x => x.ExpiryYear);
    }

    [Fact]
    public void Validate_WhenGatewayCapitalised_Fails()
    {
        var c = Valid(); c.Gateway = "Stripe";
        _validator.TestValidate(c).ShouldHaveValidationErrorFor(x => x.Gateway);
    }
}
