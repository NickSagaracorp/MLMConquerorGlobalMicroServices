using FluentValidation;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Placement.RemovePlacement;

public class RemovePlacementValidator : AbstractValidator<RemovePlacementCommand>
{
    public RemovePlacementValidator()
    {
        RuleFor(x => x.MemberToRemoveId)
            .NotEmpty().WithMessage("MemberToRemoveId is required.")
            .MaximumLength(450);
    }
}
