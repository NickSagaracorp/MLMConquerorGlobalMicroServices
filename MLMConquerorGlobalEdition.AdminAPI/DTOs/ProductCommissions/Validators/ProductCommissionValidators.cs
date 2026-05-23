using FluentValidation;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.ProductCommissions.Validators;

public class CreateProductCommissionRequestValidator : AbstractValidator<CreateProductCommissionRequest>
{
    public CreateProductCommissionRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty()
            .MaximumLength(64)
            .Matches(AdminValidationPatterns.ProductIdPattern)
                .WithMessage("ProductId must be a 36-character GUID-shaped identifier.");
    }
}

// UpdateProductCommissionRequest is bool-only; FluentValidation has nothing
// meaningful to enforce, but registering an empty validator is harmless and
// keeps the surface consistent with Create.
public class UpdateProductCommissionRequestValidator : AbstractValidator<UpdateProductCommissionRequest>
{
    public UpdateProductCommissionRequestValidator()
    {
    }
}
