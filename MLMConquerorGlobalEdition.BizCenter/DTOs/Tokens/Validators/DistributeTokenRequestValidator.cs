using FluentValidation;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Validation;

namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Tokens.Validators;

public class DistributeTokenRequestValidator : AbstractValidator<DistributeTokenRequest>
{
    public DistributeTokenRequestValidator()
    {
        RuleFor(x => x.TokenTypeId).GreaterThan(0);

        RuleFor(x => x.RecipientMemberId)
            .NotEmpty()
            .MaximumLength(32)
            .Matches(BizCenterValidationPatterns.MemberIdPattern)
                .WithMessage("RecipientMemberId must be a valid member identifier.");

        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, 10_000);
    }
}
