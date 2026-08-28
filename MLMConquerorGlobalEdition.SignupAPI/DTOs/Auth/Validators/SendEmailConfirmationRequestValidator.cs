using FluentValidation;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth.Validators;

public class SendEmailConfirmationRequestValidator : AbstractValidator<SendEmailConfirmationRequest>
{
    public SendEmailConfirmationRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(ValidationPatterns.EmailMaxLength)
            .EmailAddress()
            .Matches(ValidationPatterns.EmailPattern)
                .WithMessage("Email contains invalid characters.");
    }
}
