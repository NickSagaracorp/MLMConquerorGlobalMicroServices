using FluentValidation;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.ConfirmCryptoPayment;

public class ConfirmCryptoPaymentValidator : AbstractValidator<ConfirmCryptoPaymentCommand>
{
    public ConfirmCryptoPaymentValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty().WithMessage("OrderId is required.");

        // AQUÍ SÍ es obligatorio. Este es el único momento del flujo en el que el identificador
        // de la transferencia existe de verdad, y sin él el rastro de auditoría no permite
        // cotejar el cobro contra la cadena.
        RuleFor(x => x.Request.CryptoTransactionId)
            .NotEmpty().WithMessage("CryptoTransactionId is required to confirm a crypto payment.")
            .MaximumLength(ValidationPatterns.CryptoTxIdMaxLength)
            .Matches(ValidationPatterns.CryptoTxIdPattern)
                .WithMessage("CryptoTransactionId contains invalid characters.");

        RuleFor(x => x.Request.Notes)
            .MaximumLength(ValidationPatterns.ReasonMaxLength)
            .Matches(ValidationPatterns.SafeTextPattern)
                .WithMessage("Notes contains invalid characters.")
            .When(x => !string.IsNullOrEmpty(x.Request.Notes));

        RuleFor(x => x.ConfirmedByUserId)
            .NotEmpty().WithMessage("The approving user could not be identified.");
    }
}
