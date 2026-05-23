using FluentValidation;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Validators;

public class ValidateTokenRequestValidator : AbstractValidator<ValidateTokenRequest>
{
    public ValidateTokenRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .Matches(ValidationPatterns.CodePattern)
                .WithMessage("Code must be 4-32 uppercase alphanumeric characters or hyphens.");

        RuleFor(x => x.SponsorReplicateSite)
            .NotEmpty()
            .MaximumLength(64)
            .Must(value =>
                System.Text.RegularExpressions.Regex.IsMatch(value, ValidationPatterns.MemberIdPattern) ||
                System.Text.RegularExpressions.Regex.IsMatch(value, ValidationPatterns.ReplicateSlugPattern))
                .WithMessage("SponsorReplicateSite must be a member identifier or replicate-site slug.");

        RuleFor(x => x.SelectedProductIds)
            .NotNull()
            .Must(ids => ids.Count <= 100)
                .WithMessage("Cannot select more than 100 products at once.");

        RuleForEach(x => x.SelectedProductIds)
            .NotEmpty()
            .MaximumLength(64)
            .Matches(ValidationPatterns.ProductIdPattern)
                .WithMessage("Product IDs must be 36-character GUID-shaped identifiers.");

        RuleFor(x => x.VisitorId)
            .MaximumLength(ValidationPatterns.VisitorIdMaxLength)
            .Matches(ValidationPatterns.VisitorIdPattern)
                .WithMessage("VisitorId must be 8-64 alphanumeric characters.")
            .When(x => !string.IsNullOrEmpty(x.VisitorId));
    }
}
