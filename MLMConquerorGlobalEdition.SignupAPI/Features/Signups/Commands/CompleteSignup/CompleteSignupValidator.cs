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

        RuleFor(x => x.Request.CreditCard!.GatewayToken)
            .NotEmpty().WithMessage("Gateway token is required for credit card payments.")
            .When(x => x.Request.PaymentMethod == PaymentMethodType.CreditCard
                     && x.Request.CreditCard != null);

        RuleFor(x => x.Request.CreditCard!.Last4)
            .Length(4).WithMessage("Last 4 digits required.")
            .When(x => x.Request.PaymentMethod == PaymentMethodType.CreditCard
                     && x.Request.CreditCard != null);

        // El identificador de la transacción NO se exige aquí. Nadie puede producirlo al
        // completar el alta: la transferencia todavía no se ha hecho, o se acaba de hacer y el
        // aspirante no tiene el hash a mano. Se captura al confirmar el cobro, en
        // ConfirmCryptoPaymentValidator, que sí lo exige. Lo que sí hace falta desde el principio
        // es saber en qué moneda se va a pagar, y eso lo cubre CompleteSignupRequestValidator.
        RuleFor(x => x.Request.CryptoCurrency)
            .NotEmpty().WithMessage("Crypto currency is required.")
            .When(x => x.Request.PaymentMethod == PaymentMethodType.Crypto);

        RuleFor(x => x.Request.TokenCode)
            .NotEmpty().WithMessage("Token code is required.")
            .When(x => x.Request.PaymentMethod == PaymentMethodType.Token);

        RuleFor(x => x.Request.DiscountCode)
            .NotEmpty().WithMessage("Discount code is required.")
            .When(x => x.Request.PaymentMethod == PaymentMethodType.DiscountCode);
    }
}
