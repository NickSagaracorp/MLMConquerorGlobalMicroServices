using FluentValidation;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.MembershipLevels.Validators;

public class CreateMembershipLevelDtoValidator : AbstractValidator<CreateMembershipLevelDto>
{
    public CreateMembershipLevelDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty()
            .MaximumLength(AdminValidationPatterns.NameMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern);

        RuleFor(x => x.Description)
            .MaximumLength(AdminValidationPatterns.DescriptionMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern)
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Price).InclusiveBetween(0m, AdminValidationPatterns.AmountMax);
        RuleFor(x => x.RenewalPrice).InclusiveBetween(0m, AdminValidationPatterns.AmountMax);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpdateMembershipLevelDtoValidator : AbstractValidator<UpdateMembershipLevelDto>
{
    public UpdateMembershipLevelDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty()
            .MaximumLength(AdminValidationPatterns.NameMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern);

        RuleFor(x => x.Description)
            .MaximumLength(AdminValidationPatterns.DescriptionMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern)
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Price).InclusiveBetween(0m, AdminValidationPatterns.AmountMax);
        RuleFor(x => x.RenewalPrice).InclusiveBetween(0m, AdminValidationPatterns.AmountMax);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
