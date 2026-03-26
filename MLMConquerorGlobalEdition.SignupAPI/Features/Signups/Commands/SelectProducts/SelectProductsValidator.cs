using FluentValidation;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.SelectProducts;

public class SelectProductsValidator : AbstractValidator<SelectProductsCommand>
{
    public SelectProductsValidator()
    {
        RuleFor(x => x.SignupId)
            .NotEmpty().WithMessage("SignupId is required.");

        RuleFor(x => x.Request.ProductIds)
            .NotEmpty().WithMessage("At least one product must be selected.");

        RuleForEach(x => x.Request.ProductIds)
            .NotEmpty().WithMessage("Product ID cannot be empty.");
    }
}
