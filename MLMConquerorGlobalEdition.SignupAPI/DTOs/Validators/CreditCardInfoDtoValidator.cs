using FluentValidation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Validators;

public class CreditCardInfoDtoValidator : AbstractValidator<CreditCardInfoDto>
{
    public CreditCardInfoDtoValidator()
    {
        // GatewayToken & CardToken are opaque gateway-issued blobs. Cap to a
        // generous length and restrict to a safe ASCII charset.
        RuleFor(x => x.GatewayToken)
            .NotEmpty()
            .MaximumLength(512)
            .Matches(@"^[A-Za-z0-9_\-\.:]+$")
                .WithMessage("GatewayToken contains invalid characters.");

        RuleFor(x => x.CardToken)
            .NotEmpty()
            .MaximumLength(512)
            .Matches(@"^[A-Za-z0-9_\-\.:]+$")
                .WithMessage("CardToken contains invalid characters.");

        RuleFor(x => x.Last4)
            .NotEmpty()
            .Matches(@"^\d{4}$")
                .WithMessage("Last4 must be exactly 4 digits.");

        RuleFor(x => x.First6)
            .NotEmpty()
            .Matches(@"^\d{6}$")
                .WithMessage("First6 must be exactly 6 digits.");

        RuleFor(x => x.CardBrand)
            .NotEmpty()
            .MaximumLength(30)
            .Matches(@"^[A-Za-z ]{1,30}$")
                .WithMessage("CardBrand contains invalid characters.");

        RuleFor(x => x.ExpiryMonth)
            .InclusiveBetween(1, 12);

        RuleFor(x => x.ExpiryYear)
            .InclusiveBetween(DateTime.UtcNow.Year, DateTime.UtcNow.Year + 30);

        RuleFor(x => x.Gateway)
            .NotEmpty()
            .MaximumLength(30)
            .Matches(@"^[a-z][a-z0-9]{1,29}$")
                .WithMessage("Gateway must be lowercase alphanumeric (e.g. 'stripe', 'braintree').");
    }
}
