using FluentValidation;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Validators;

public class CompleteSignupRequestValidator : AbstractValidator<CompleteSignupRequest>
{
    public CompleteSignupRequestValidator()
    {
        RuleFor(x => x.PaymentMethod)
            .IsInEnum();

        // CreditCard sub-DTO when payment is by card.
        When(x => x.PaymentMethod == PaymentMethodType.CreditCard, () =>
        {
            RuleFor(x => x.CreditCard)
                .NotNull().WithMessage("CreditCard is required when PaymentMethod is CreditCard.");
            RuleFor(x => x.CreditCard!)
                .SetValidator(new CreditCardInfoDtoValidator())
                .When(x => x.CreditCard is not null);
        });

        // Crypto fields when payment is by crypto.
        When(x => x.PaymentMethod == PaymentMethodType.Crypto, () =>
        {
            RuleFor(x => x.CryptoCurrency)
                .NotEmpty().WithMessage("CryptoCurrency is required when PaymentMethod is Crypto.")
                .MaximumLength(ValidationPatterns.CryptoCurrencyMaxLength)
                .Matches(ValidationPatterns.CryptoCurrencyPattern)
                    .WithMessage("CryptoCurrency contains invalid characters.");

            // Opcional al completar el alta: el cobro en cripto llega por fuera y el hash lo
            // captura quien lo confirma. Si de todas formas viniera en la petición —un cliente
            // antiguo, una prueba— se sigue exigiendo que tenga la forma correcta, porque un
            // valor con basura dentro no es mejor que ninguno.
            RuleFor(x => x.CryptoTransactionId)
                .MaximumLength(ValidationPatterns.CryptoTxIdMaxLength)
                .Matches(ValidationPatterns.CryptoTxIdPattern)
                    .WithMessage("CryptoTransactionId contains invalid characters.")
                .When(x => !string.IsNullOrEmpty(x.CryptoTransactionId));
        });

        When(x => x.PaymentMethod == PaymentMethodType.Token, () =>
        {
            RuleFor(x => x.TokenCode)
                .NotEmpty().WithMessage("TokenCode is required when PaymentMethod is Token.")
                .Matches(ValidationPatterns.CodePattern)
                    .WithMessage("TokenCode must be 4-32 uppercase alphanumeric characters or hyphens.");
        });

        When(x => x.PaymentMethod == PaymentMethodType.DiscountCode, () =>
        {
            RuleFor(x => x.DiscountCode)
                .NotEmpty().WithMessage("DiscountCode is required when PaymentMethod is DiscountCode.")
                .Matches(ValidationPatterns.CodePattern)
                    .WithMessage("DiscountCode must be 4-32 uppercase alphanumeric characters or hyphens.");
        });

        RuleFor(x => x.CheckoutScreenshotContentType)
            .NotEmpty()
            .MaximumLength(50)
            .Matches(ValidationPatterns.ImageContentTypePattern)
                .WithMessage("CheckoutScreenshotContentType must be image/png, image/jpeg or image/webp.");

        RuleFor(x => x.CheckoutScreenshotBase64)
            .MaximumLength(ValidationPatterns.Base64MaxLength)
                .WithMessage("Screenshot payload exceeds the maximum allowed size.")
            .Matches(ValidationPatterns.Base64Pattern)
                .WithMessage("Screenshot must be valid base64 (with optional data: prefix).")
            .When(x => !string.IsNullOrEmpty(x.CheckoutScreenshotBase64));
    }
}
