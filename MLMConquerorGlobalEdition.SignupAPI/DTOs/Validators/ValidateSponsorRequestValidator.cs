using FluentValidation;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Validators;

public class ValidateSponsorRequestValidator : AbstractValidator<ValidateSponsorRequest>
{
    public ValidateSponsorRequestValidator()
    {
        // The endpoint accepts either a MemberId (AMB-/MBR-/ROOT) or a
        // replicate-site slug — both formats are common entry points on the
        // join page.
        RuleFor(x => x.SponsorMemberId)
            .NotEmpty()
            .MaximumLength(64)
            .Must(value =>
                System.Text.RegularExpressions.Regex.IsMatch(value, ValidationPatterns.MemberIdPattern) ||
                System.Text.RegularExpressions.Regex.IsMatch(value, ValidationPatterns.ReplicateSlugPattern))
                .WithMessage("SponsorMemberId must be a member identifier (AMB-/MBR-/ROOT) or a valid replicate-site slug.");
    }
}
