using FluentValidation.TestHelper;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Validators;

namespace MLMConquerorGlobalEdition.Signups.Tests.Validators;

public class CreditCardInfoDtoValidatorTests
{
    private readonly CreditCardInfoDtoValidator _validator = new();

    private static CreditCardInfoDto Valid() => new()
    {
        CardHolderFirstName = "Jane",
        CardHolderLastName  = "Doe",
        CardNumber          = "4111111111111111",
        Cvv                 = "123",
        ExpiryMonth         = 12,
        ExpiryYear          = DateTime.UtcNow.Year + 2,
    };

    [Fact]
    public void Validate_WhenAllFieldsValid_Passes()
        => _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenCardHolderFirstNameEmpty_Fails()
    {
        var c = Valid(); c.CardHolderFirstName = "";
        _validator.TestValidate(c).ShouldHaveValidationErrorFor(x => x.CardHolderFirstName);
    }

    [Fact]
    public void Validate_WhenCardNumberTooShort_Fails()
    {
        var c = Valid(); c.CardNumber = "12345";
        _validator.TestValidate(c).ShouldHaveValidationErrorFor(x => x.CardNumber);
    }

    [Fact]
    public void Validate_WhenCardNumberContainsNonDigits_Fails()
    {
        var c = Valid(); c.CardNumber = "4111-1111-1111-1111";
        _validator.TestValidate(c).ShouldHaveValidationErrorFor(x => x.CardNumber);
    }

    [Fact]
    public void Validate_WhenCvvNotThreeOrFourDigits_Fails()
    {
        var c = Valid(); c.Cvv = "12";
        _validator.TestValidate(c).ShouldHaveValidationErrorFor(x => x.Cvv);
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
}
