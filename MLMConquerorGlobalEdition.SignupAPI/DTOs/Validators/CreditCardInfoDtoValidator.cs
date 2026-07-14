using FluentValidation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Validators;

public class CreditCardInfoDtoValidator : AbstractValidator<CreditCardInfoDto>
{
    public CreditCardInfoDtoValidator()
    {
        RuleFor(x => x.CardHolderFirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.CardHolderLastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.CardNumber)
            .NotEmpty()
            .Matches(@"^\d{12,19}$")
                .WithMessage("Card number must be 12-19 digits.");

        RuleFor(x => x.Cvv)
            .NotEmpty()
            .Matches(@"^\d{3,4}$")
                .WithMessage("CVV must be 3-4 digits.");

        RuleFor(x => x.ExpiryMonth)
            .InclusiveBetween(1, 12);

        RuleFor(x => x.ExpiryYear)
            .InclusiveBetween(DateTime.UtcNow.Year, DateTime.UtcNow.Year + 30);
    }
}
