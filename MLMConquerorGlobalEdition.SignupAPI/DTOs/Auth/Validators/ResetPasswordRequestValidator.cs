using FluentValidation;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth.Validators;

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(ValidationPatterns.EmailMaxLength)
            .EmailAddress()
            .Matches(ValidationPatterns.EmailPattern);

        RuleFor(x => x.Token)
            .NotEmpty()
            // Identity reset tokens are base64url-ish; cap aggressively.
            .MaximumLength(4096)
            .Matches(@"^[A-Za-z0-9_\-\.=+/%]+$")
                .WithMessage("Reset token contains invalid characters.");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(ValidationPatterns.PasswordMinLength)
            .MaximumLength(ValidationPatterns.PasswordMaxLength)
            .Matches(ValidationPatterns.PasswordPattern)
                .WithMessage("Password must contain at least one digit, one uppercase letter, one lowercase letter and one special character.");
    }
}
