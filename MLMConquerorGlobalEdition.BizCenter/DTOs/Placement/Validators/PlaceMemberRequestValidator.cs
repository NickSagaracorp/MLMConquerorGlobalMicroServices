using FluentValidation;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Validation;

namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Placement.Validators;

public class PlaceMemberRequestValidator : AbstractValidator<PlaceMemberRequest>
{
    public PlaceMemberRequestValidator()
    {
        RuleFor(x => x.MemberToPlaceId)
            .NotEmpty()
            .MaximumLength(32)
            .Matches(BizCenterValidationPatterns.MemberIdPattern);

        RuleFor(x => x.TargetParentMemberId)
            .NotEmpty()
            .MaximumLength(32)
            .Matches(BizCenterValidationPatterns.MemberIdPattern);

        RuleFor(x => x.Side)
            .NotEmpty()
            .Matches(BizCenterValidationPatterns.PlacementSidePattern);
    }
}
