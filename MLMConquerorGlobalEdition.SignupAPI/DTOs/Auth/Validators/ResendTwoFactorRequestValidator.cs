using FluentValidation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth.Validators;

public class ResendTwoFactorRequestValidator : AbstractValidator<ResendTwoFactorRequest>
{
    public ResendTwoFactorRequestValidator()
    {
        RuleFor(x => x.ChallengeToken)
            .NotEmpty()
            .MaximumLength(4096)
            .Matches(@"^[A-Za-z0-9_\-\.=]+$")
                .WithMessage("Challenge token contains invalid characters.");
    }
}
