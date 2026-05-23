using FluentValidation;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(ValidationPatterns.EmailMaxLength)
            .EmailAddress()
            .Matches(ValidationPatterns.EmailPattern)
                .WithMessage("Email contains invalid characters.");

        // Login itself does not enforce password complexity — only length cap
        // (anti-DoS); strength is enforced at registration / change.
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(1)
            .MaximumLength(ValidationPatterns.PasswordMaxLength);
    }
}
