using FluentValidation;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.SignupMember;

public class SignupMemberValidator : AbstractValidator<SignupMemberCommand>
{
    public SignupMemberValidator()
    {
        RuleFor(x => x.Request.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Request.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.Request.Country).NotEmpty().MaximumLength(100);

        When(x => IsUnitedStates(x.Request.Country), () =>
        {
            RuleFor(x => x.Request.Ssn)
                .NotEmpty().WithMessage("SSN is required for United States residents.")
                .Matches(@"^\d{3}-\d{2}-\d{4}$").WithMessage("SSN must be in the format XXX-XX-XXXX.");
        });
    }

    private static bool IsUnitedStates(string country)
        => !string.IsNullOrWhiteSpace(country) &&
           (country.Equals("US", StringComparison.OrdinalIgnoreCase) ||
            country.Equals("United States", StringComparison.OrdinalIgnoreCase) ||
            country.Equals("United States of America", StringComparison.OrdinalIgnoreCase));
}
