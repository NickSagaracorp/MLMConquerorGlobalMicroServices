using FluentValidation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth.Validators;

public class BeginEnrollmentRequestValidator : AbstractValidator<BeginEnrollmentRequest>
{
    public BeginEnrollmentRequestValidator()
    {
        RuleFor(x => x.EnrollmentToken)
            .NotEmpty()
            .MaximumLength(4096)
            .Matches(@"^[A-Za-z0-9_\-\.=]+$")
                .WithMessage("Enrollment token contains invalid characters.");
    }
}
