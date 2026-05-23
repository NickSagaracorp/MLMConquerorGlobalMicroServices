using FluentValidation;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Validators;

public class PlacementRequestValidator : AbstractValidator<PlacementRequest>
{
    public PlacementRequestValidator()
    {
        RuleFor(x => x.PlaceUnderMemberId)
            .NotEmpty()
            .MaximumLength(32)
            .Matches(ValidationPatterns.MemberIdPattern)
                .WithMessage("PlaceUnderMemberId must be a valid member identifier (AMB-DDDDDD, MBR-DDDDDD or ROOTDDD).");

        RuleFor(x => x.Side)
            .NotEmpty()
            .Matches(ValidationPatterns.PlacementSidePattern)
                .WithMessage("Side must be 'Left' or 'Right'.");
    }
}
