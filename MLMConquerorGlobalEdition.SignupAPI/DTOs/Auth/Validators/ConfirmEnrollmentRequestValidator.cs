using FluentValidation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth.Validators;

public class ConfirmEnrollmentRequestValidator : AbstractValidator<ConfirmEnrollmentRequest>
{
    public ConfirmEnrollmentRequestValidator()
    {
        RuleFor(x => x.EnrollmentToken)
            .NotEmpty()
            .MaximumLength(4096)
            .Matches(@"^[A-Za-z0-9_\-\.=]+$")
                .WithMessage("Enrollment token contains invalid characters.");

        // Seis dígitos: es lo que produce la aplicación de autenticación (digits=6 en el
        // otpauth:// que emite el enrolamiento).
        RuleFor(x => x.Code)
            .NotEmpty()
            .Matches(@"^\d{6}$")
                .WithMessage("Verification code must be exactly 6 digits.");
    }
}
