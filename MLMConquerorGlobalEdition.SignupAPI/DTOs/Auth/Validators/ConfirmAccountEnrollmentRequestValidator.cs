using FluentValidation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth.Validators;

public class ConfirmAccountEnrollmentRequestValidator : AbstractValidator<ConfirmAccountEnrollmentRequest>
{
    public ConfirmAccountEnrollmentRequestValidator()
    {
        // Seis dígitos: es lo que produce la aplicación de autenticación (digits=6 en el
        // otpauth:// que emite el enrolamiento).
        RuleFor(x => x.Code)
            .NotEmpty()
            .Matches(@"^\d{6}$")
                .WithMessage("Verification code must be exactly 6 digits.");
    }
}
