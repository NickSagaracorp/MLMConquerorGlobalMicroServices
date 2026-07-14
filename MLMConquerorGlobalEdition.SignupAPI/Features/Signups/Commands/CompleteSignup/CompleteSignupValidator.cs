using FluentValidation;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.CompleteSignup;

public class CompleteSignupValidator : AbstractValidator<CompleteSignupCommand>
{
    public CompleteSignupValidator()
    {
        RuleFor(x => x.SignupId)
            .NotEmpty().WithMessage("SignupId is required.");

        RuleFor(x => x.Request.PaymentMethod)
            .IsInEnum().WithMessage("Invalid payment method.");

        RuleFor(x => x.Request.CreditCard)
            .NotNull().WithMessage("Credit card info is required.")
            .When(x => x.Request.PaymentMethod == PaymentMethodType.CreditCard);

        RuleFor(x => x.Request.CreditCard!.CardNumber)
            .Matches(@"^\d{12,19}$").WithMessage("Card number must be 12-19 digits.")
            .When(x => x.Request.PaymentMethod == PaymentMethodType.CreditCard
                     && x.Request.CreditCard != null);

        RuleFor(x => x.Request.CreditCard!.Cvv)
            .Matches(@"^\d{3,4}$").WithMessage("CVV must be 3-4 digits.")
            .When(x => x.Request.PaymentMethod == PaymentMethodType.CreditCard
                     && x.Request.CreditCard != null);

        RuleFor(x => x.Request.CreditCard!.ExpiryMonth)
            .InclusiveBetween(1, 12).WithMessage("Expiry month must be between 1 and 12.")
            .When(x => x.Request.PaymentMethod == PaymentMethodType.CreditCard
                     && x.Request.CreditCard != null);

        RuleFor(x => x.Request.CreditCard)
            .Must(cc => cc!.ExpiryYear > DateTime.UtcNow.Year ||
                        (cc.ExpiryYear == DateTime.UtcNow.Year && cc.ExpiryMonth >= DateTime.UtcNow.Month))
            .WithMessage("Card has expired.")
            .When(x => x.Request.PaymentMethod == PaymentMethodType.CreditCard
                     && x.Request.CreditCard != null);

        RuleFor(x => x.Request.CryptoTransactionId)
            .NotEmpty().WithMessage("Crypto transaction ID is required.")
            .When(x => x.Request.PaymentMethod == PaymentMethodType.Crypto);

        RuleFor(x => x.Request.TokenCode)
            .NotEmpty().WithMessage("Token code is required.")
            .When(x => x.Request.PaymentMethod == PaymentMethodType.Token);

        RuleFor(x => x.Request.DiscountCode)
            .NotEmpty().WithMessage("Discount code is required.")
            .When(x => x.Request.PaymentMethod == PaymentMethodType.DiscountCode);
    }
}
