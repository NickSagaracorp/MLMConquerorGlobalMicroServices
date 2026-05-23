using FluentValidation;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth.Validators;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .MaximumLength(ValidationPatterns.PasswordMaxLength);

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(ValidationPatterns.PasswordMinLength)
            .MaximumLength(ValidationPatterns.PasswordMaxLength)
            .Matches(ValidationPatterns.PasswordPattern)
                .WithMessage("Password must contain at least one digit, one uppercase letter, one lowercase letter and one special character.");
    }
}
