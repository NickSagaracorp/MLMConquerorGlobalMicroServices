using FluentValidation;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Validation;

namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Billing.Validators;

public class AddCreditCardRequestValidator : AbstractValidator<AddCreditCardRequest>
{
    public AddCreditCardRequestValidator()
    {
        RuleFor(x => x.CardNumber)
            .NotEmpty()
            .Matches(BizCenterValidationPatterns.CreditCardPanPattern)
                .WithMessage("CardNumber must be 13-19 digits with no separators.");

        RuleFor(x => x.CardholderName)
            .NotEmpty()
            .MaximumLength(BizCenterValidationPatterns.CardholderNameMaxLength)
            .Matches(BizCenterValidationPatterns.CardholderNamePattern)
                .WithMessage("CardholderName must start with a letter and contain only letters, spaces, hyphens, apostrophes or periods.");

        RuleFor(x => x.ExpiryMonth).InclusiveBetween(1, 12);
        RuleFor(x => x.ExpiryYear).InclusiveBetween(DateTime.UtcNow.Year, DateTime.UtcNow.Year + 30);

        RuleFor(x => x.Cvv)
            .NotEmpty()
            .Matches(BizCenterValidationPatterns.CreditCardCvvPattern)
                .WithMessage("CVV must be 3 or 4 digits.");
    }
}

public class ReorderCreditCardsRequestValidator : AbstractValidator<ReorderCreditCardsRequest>
{
    public ReorderCreditCardsRequestValidator()
    {
        RuleFor(x => x.OrderedCardIds)
            .NotNull()
            .Must(ids => ids.Count > 0 && ids.Count <= 100)
                .WithMessage("OrderedCardIds must contain between 1 and 100 entries.");

        RuleForEach(x => x.OrderedCardIds)
            .NotEmpty()
            .MaximumLength(64)
            .Matches(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")
                .WithMessage("Credit card IDs must be GUIDs.");
    }
}
