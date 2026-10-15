using FluentValidation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth.Validators;

public class StepUpChallengeRequestValidator : AbstractValidator<StepUpChallengeRequest>
{
    public StepUpChallengeRequestValidator()
    {
        RuleFor(x => x.OperationKey).NotEmpty().MaximumLength(64);
    }
}
