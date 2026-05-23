using FluentValidation;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Validators;

public class ValidateReplicateSiteRequestValidator : AbstractValidator<ValidateReplicateSiteRequest>
{
    public ValidateReplicateSiteRequestValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(ValidationPatterns.ReplicateSlugMaxLength)
            .Matches(ValidationPatterns.ReplicateSlugPattern)
                .WithMessage("Slug must be lowercase alphanumeric with hyphens (2-50 chars), no leading or trailing hyphen.");
    }
}
