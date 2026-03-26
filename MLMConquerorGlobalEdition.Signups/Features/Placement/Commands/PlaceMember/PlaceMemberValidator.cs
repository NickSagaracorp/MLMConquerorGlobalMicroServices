using FluentValidation;

namespace MLMConquerorGlobalEdition.Signups.Features.Placement.Commands.PlaceMember;

public class PlaceMemberValidator : AbstractValidator<PlaceMemberCommand>
{
    public PlaceMemberValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.PlaceUnderMemberId).NotEmpty();
        RuleFor(x => x.Side).NotEmpty().Must(s => s == "Left" || s == "Right")
            .WithMessage("Side must be 'Left' or 'Right'.");
    }
}
