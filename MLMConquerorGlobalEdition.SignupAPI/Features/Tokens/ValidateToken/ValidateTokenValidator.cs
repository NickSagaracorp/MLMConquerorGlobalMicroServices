using FluentValidation;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Tokens.ValidateToken;

public class ValidateTokenValidator : AbstractValidator<ValidateTokenQuery>
{
    public ValidateTokenValidator()
    {
        RuleFor(x => x.Request.Code)
            .NotEmpty().WithMessage("Token code is required.")
            .MaximumLength(20);

        RuleFor(x => x.Request.SponsorReplicateSite)
            .NotEmpty().WithMessage("Sponsor is required.");
    }
}
