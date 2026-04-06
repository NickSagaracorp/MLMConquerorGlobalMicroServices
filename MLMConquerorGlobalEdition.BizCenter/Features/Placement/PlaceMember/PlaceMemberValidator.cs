using FluentValidation;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Placement.PlaceMember;

public class PlaceMemberValidator : AbstractValidator<PlaceMemberCommand>
{
    public PlaceMemberValidator()
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

        // Prevent targeting self in the same request (structural rule also in handler,
        // but catching it early here produces a cleaner validation error)
        RuleFor(x => x)
            .Must(x => x.MemberToPlaceId != x.TargetParentMemberId)
            .WithMessage("A member cannot be placed under themselves.")
            .OverridePropertyName("TargetParentMemberId");
    }
}
