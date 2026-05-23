using FluentValidation;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenTypeCommissions.Validators;

public class CreateTokenTypeCommissionRequestValidator : AbstractValidator<CreateTokenTypeCommissionRequest>
{
    public CreateTokenTypeCommissionRequestValidator()
    {
        RuleFor(x => x.TokenTypeId).GreaterThan(0);
        RuleFor(x => x.CommissionTypeId).GreaterThan(0);
        RuleFor(x => x.CommissionPerToken)
            .InclusiveBetween(0m, AdminValidationPatterns.AmountMax)
            .PrecisionScale(18, 4, true);
    }
}

public class UpdateTokenTypeCommissionRequestValidator : AbstractValidator<UpdateTokenTypeCommissionRequest>
{
    public UpdateTokenTypeCommissionRequestValidator()
    {
        RuleFor(x => x.CommissionTypeId).GreaterThan(0);
        RuleFor(x => x.CommissionPerToken)
            .InclusiveBetween(0m, AdminValidationPatterns.AmountMax)
            .PrecisionScale(18, 4, true);
    }
}
