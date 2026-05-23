using FluentValidation;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.ProductLoyaltyPoints.Validators;

public class CreateProductLoyaltyPointsRequestValidator : AbstractValidator<CreateProductLoyaltyPointsRequest>
{
    public CreateProductLoyaltyPointsRequestValidator()
    {
        RuleFor(x => x.PointsPerUnit)
            .InclusiveBetween(0m, AdminValidationPatterns.AmountMax);
        RuleFor(x => x.RequiredSuccessfulPayments)
            .InclusiveBetween(0, 10_000);
    }
}

public class UpdateProductLoyaltyPointsRequestValidator : AbstractValidator<UpdateProductLoyaltyPointsRequest>
{
    public UpdateProductLoyaltyPointsRequestValidator()
    {
        RuleFor(x => x.PointsPerUnit)
            .InclusiveBetween(0m, AdminValidationPatterns.AmountMax);
        RuleFor(x => x.RequiredSuccessfulPayments)
            .InclusiveBetween(0, 10_000);
    }
}
