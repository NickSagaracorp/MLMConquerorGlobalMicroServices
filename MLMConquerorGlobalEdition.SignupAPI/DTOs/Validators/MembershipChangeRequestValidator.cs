using FluentValidation;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Validators;

public class MembershipChangeRequestValidator : AbstractValidator<MembershipChangeRequest>
{
    public MembershipChangeRequestValidator()
    {
        RuleFor(x => x.NewMembershipLevelId)
            .GreaterThan(0);

        RuleFor(x => x.Reason)
            .MaximumLength(ValidationPatterns.ReasonMaxLength)
            .Matches(ValidationPatterns.SafeTextPattern)
                .WithMessage("Reason contains invalid characters.")
            .When(x => !string.IsNullOrEmpty(x.Reason));
    }
}
