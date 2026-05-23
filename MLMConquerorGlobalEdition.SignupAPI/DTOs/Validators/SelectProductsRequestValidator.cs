using FluentValidation;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Validators;

public class SelectProductsRequestValidator : AbstractValidator<SelectProductsRequest>
{
    public SelectProductsRequestValidator()
    {
        RuleFor(x => x.ProductIds)
            .NotNull()
            .Must(ids => ids.Count <= 100)
                .WithMessage("Cannot select more than 100 products at once.");

        RuleForEach(x => x.ProductIds)
            .NotEmpty()
            .MaximumLength(64)
            .Matches(ValidationPatterns.ProductIdPattern)
                .WithMessage("Product IDs must be 36-character GUID-shaped identifiers.");
    }
}
