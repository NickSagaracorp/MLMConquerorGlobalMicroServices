using FluentValidation;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.GhostPoints.Validators;

public class CreateGhostPointRequestValidator : AbstractValidator<CreateGhostPointRequest>
{
    public CreateGhostPointRequestValidator()
    {
        RuleFor(x => x.MemberId)
            .NotEmpty()
            .MaximumLength(32)
            .Matches(AdminValidationPatterns.MemberIdPattern)
                .WithMessage("MemberId must be a valid member identifier.");

        RuleFor(x => x.LegMemberId)
            .NotEmpty()
            .MaximumLength(32)
            .Matches(AdminValidationPatterns.MemberIdPattern)
                .WithMessage("LegMemberId must be a valid member identifier.");

        RuleFor(x => x.Points)
            .NotEqual(0).WithMessage("Points must be non-zero.")
            .InclusiveBetween(-1_000_000, 1_000_000);

        RuleFor(x => x.Side).IsInEnum();

        RuleFor(x => x.Notes)
            .MaximumLength(AdminValidationPatterns.NotesMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern)
                .WithMessage("Notes contains invalid characters.")
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}
