using FluentValidation;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.GlobalParameters.Validators;

public class CreateGlobalParameterRequestValidator : AbstractValidator<CreateGlobalParameterRequest>
{
    public CreateGlobalParameterRequestValidator()
    {
        RuleFor(x => x.Key).NotEmpty().Matches(GlobalParameterRules.KeyPattern)
            .WithMessage("Key may only contain letters, digits, dots, dashes and underscores (max 200).");
        RuleFor(x => x.Value).NotNull().MaximumLength(GlobalParameterRules.ValueMaxLength);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class UpdateGlobalParameterRequestValidator : AbstractValidator<UpdateGlobalParameterRequest>
{
    public UpdateGlobalParameterRequestValidator()
    {
        RuleFor(x => x.Value).NotNull().MaximumLength(GlobalParameterRules.ValueMaxLength);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
