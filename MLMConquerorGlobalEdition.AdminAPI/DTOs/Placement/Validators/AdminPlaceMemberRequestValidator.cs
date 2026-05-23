using FluentValidation;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Placement.Validators;

public class AdminPlaceMemberRequestValidator : AbstractValidator<AdminPlaceMemberRequest>
{
    public AdminPlaceMemberRequestValidator()
    {
        RuleFor(x => x.MemberToPlaceId)
            .NotEmpty()
            .MaximumLength(32)
            .Matches(AdminValidationPatterns.MemberIdPattern);

        RuleFor(x => x.TargetParentMemberId)
            .NotEmpty()
            .MaximumLength(32)
            .Matches(AdminValidationPatterns.MemberIdPattern);

        RuleFor(x => x.Side)
            .NotEmpty()
            .Matches(AdminValidationPatterns.PlacementSidePattern);
    }
}
