using FluentValidation;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Placement.AdminPlaceMember;

public class AdminPlaceMemberValidator : AbstractValidator<AdminPlaceMemberCommand>
{
    public AdminPlaceMemberValidator()
    {
        RuleFor(x => x.MemberToPlaceId)
            .NotEmpty().WithMessage("MemberToPlaceId is required.")
            .MaximumLength(450);

        RuleFor(x => x.TargetParentMemberId)
            .NotEmpty().WithMessage("TargetParentMemberId is required.")
            .MaximumLength(450);

        RuleFor(x => x.Side)
            .NotEmpty().WithMessage("Side is required.")
            .Must(s => s == "Left" || s == "Right")
            .WithMessage("Side must be 'Left' or 'Right'.");

        RuleFor(x => x)
            .Must(x => x.MemberToPlaceId != x.TargetParentMemberId)
            .WithMessage("A member cannot be placed under themselves.")
            .OverridePropertyName("TargetParentMemberId");
    }
}
