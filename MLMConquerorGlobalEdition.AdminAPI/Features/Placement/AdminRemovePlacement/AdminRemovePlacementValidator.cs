using FluentValidation;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Placement.AdminRemovePlacement;

public class AdminRemovePlacementValidator : AbstractValidator<AdminRemovePlacementCommand>
{
    public AdminRemovePlacementValidator()
    {
        RuleFor(x => x.MemberId)
            .NotEmpty().WithMessage("MemberId is required.")
            .MaximumLength(450);
    }
}
