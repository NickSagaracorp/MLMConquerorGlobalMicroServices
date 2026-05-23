using FluentValidation;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth.Validators;

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(ValidationPatterns.EmailMaxLength)
            .EmailAddress()
            .Matches(ValidationPatterns.EmailPattern)
                .WithMessage("Email contains invalid characters.");
    }
}
