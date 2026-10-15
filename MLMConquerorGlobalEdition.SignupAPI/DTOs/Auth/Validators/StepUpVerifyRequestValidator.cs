using FluentValidation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth.Validators;

public class StepUpVerifyRequestValidator : AbstractValidator<StepUpVerifyRequest>
{
    public StepUpVerifyRequestValidator()
    {
        RuleFor(x => x.OperationKey).NotEmpty().MaximumLength(64);

        RuleFor(x => x.ChallengeToken)
            .NotEmpty()
            .MaximumLength(4096)
            .Matches(@"^[A-Za-z0-9_\-\.=]+$")
                .WithMessage("Challenge token contains invalid characters.");

        RuleFor(x => x.Code)
            .NotEmpty()
            .Matches(@"^\d{6}$")
                .WithMessage("Verification code must be exactly 6 digits.");
    }
}
