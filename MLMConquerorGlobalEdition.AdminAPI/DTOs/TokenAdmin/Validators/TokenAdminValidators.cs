using FluentValidation;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenAdmin.Validators;

public class AdminGrantTokenRequestValidator : AbstractValidator<AdminGrantTokenRequest>
{
    public AdminGrantTokenRequestValidator()
    {
        RuleFor(x => x.MemberId)
            .NotEmpty()
            .MaximumLength(32)
            .Matches(AdminValidationPatterns.MemberIdPattern);

        RuleFor(x => x.TokenTypeId).GreaterThan(0);

        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, 100_000);

        RuleFor(x => x.Notes)
            .MaximumLength(AdminValidationPatterns.NotesMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern)
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}

public class AdminUpdateTokenBalanceRequestValidator : AbstractValidator<AdminUpdateTokenBalanceRequest>
{
    public AdminUpdateTokenBalanceRequestValidator()
    {
        RuleFor(x => x.Balance)
            .InclusiveBetween(0, 10_000_000);
    }
}
