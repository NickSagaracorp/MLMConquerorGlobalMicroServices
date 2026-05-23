using FluentValidation;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Tokens.Validators;

public class CreateTokenTypeDtoValidator : AbstractValidator<CreateTokenTypeDto>
{
    public CreateTokenTypeDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty()
            .MaximumLength(AdminValidationPatterns.NameMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern);

        RuleFor(x => x.Description)
            .MaximumLength(AdminValidationPatterns.DescriptionMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern)
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.TemplateUrl)
            .MaximumLength(AdminValidationPatterns.UrlMaxLength)
            .Matches(AdminValidationPatterns.UrlPattern)
                .WithMessage("TemplateUrl must be a valid http/https URL.")
            .When(x => !string.IsNullOrEmpty(x.TemplateUrl));

        RuleFor(x => x.Category).IsInEnum();
    }
}

public class UpdateTokenTypeDtoValidator : AbstractValidator<UpdateTokenTypeDto>
{
    public UpdateTokenTypeDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty()
            .MaximumLength(AdminValidationPatterns.NameMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern);

        RuleFor(x => x.Description)
            .MaximumLength(AdminValidationPatterns.DescriptionMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern)
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.TemplateUrl)
            .MaximumLength(AdminValidationPatterns.UrlMaxLength)
            .Matches(AdminValidationPatterns.UrlPattern)
            .When(x => !string.IsNullOrEmpty(x.TemplateUrl));

        RuleFor(x => x.Category).IsInEnum();
    }
}
