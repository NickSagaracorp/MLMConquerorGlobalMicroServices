using FluentValidation;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Tokens.Validators;

/// <summary>Forma de la petición antes de molestar a Billing. Precio, cupo, saldo y dirección cripto los decide Billing.</summary>
public class PurchaseTokensRequestValidator : AbstractValidator<PurchaseTokensRequest>
{
    private static readonly string[] Methods =
    [
        TokenPaymentMethods.CommissionBalance, TokenPaymentMethods.EWallet,
        TokenPaymentMethods.CreditCard, TokenPaymentMethods.Crypto
    ];

    public PurchaseTokensRequestValidator()
    {
        RuleFor(x => x.TokenTypeId).GreaterThan(0);
        RuleFor(x => x.Quantity).InclusiveBetween(1, TokenPurchaseRules.MonthlyLimit);
        RuleFor(x => x.PaymentMethod)
            .Must(m => Methods.Contains(m, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Unsupported payment method.");
        RuleFor(x => x.IdempotencyKey).Must(TokenPurchaseRules.IsValidIdempotencyKey).WithMessage("Invalid idempotency key.");
        RuleFor(x => x.CreditCardId).NotEmpty().MaximumLength(450)
            .When(x => string.Equals(x.PaymentMethod, TokenPaymentMethods.CreditCard, StringComparison.OrdinalIgnoreCase));
        RuleFor(x => x.CryptoCurrency)
            .Must(c => c is not null && TokenPurchaseRules.CryptoCurrencies.Contains(c.Trim().ToUpperInvariant()))
            .When(x => string.Equals(x.PaymentMethod, TokenPaymentMethods.Crypto, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Unsupported crypto currency.");
    }
}
