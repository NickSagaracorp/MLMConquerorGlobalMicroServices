using FluentValidation;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.SignupAmbassador;

public class SignupAmbassadorValidator : AbstractValidator<SignupAmbassadorCommand>
{
    public SignupAmbassadorValidator()
    {
        RuleFor(x => x.Request.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Request.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.Request.ConfirmPassword)
            .NotEmpty()
            .Equal(x => x.Request.Password).WithMessage("Passwords do not match.");

        // Age verification — no minors allowed by law
        RuleFor(x => x.Request.DateOfBirth)
            .NotEmpty()
            .Must(dob => DateTime.Today.AddYears(-18) >= dob)
            .WithMessage("Applicant must be at least 18 years old.");

        RuleFor(x => x.Request.Phone)
            .MaximumLength(30)
            .When(x => !string.IsNullOrEmpty(x.Request.Phone));
        RuleFor(x => x.Request.WhatsApp)
            .MaximumLength(30)
            .When(x => !string.IsNullOrEmpty(x.Request.WhatsApp));

        RuleFor(x => x.Request.Country).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.ZipCode).MaximumLength(20).When(x => !string.IsNullOrEmpty(x.Request.ZipCode));

        RuleFor(x => x.Request.MembershipLevelId)
            .GreaterThan(0).WithMessage("A valid membership level is required.");

        RuleFor(x => x.Request.ReplicateSiteSlug)
            .MaximumLength(100)
            .Matches("^[a-z0-9-]+$").WithMessage("Replicate site slug must be lowercase alphanumeric with hyphens.")
            .When(x => !string.IsNullOrEmpty(x.Request.ReplicateSiteSlug));

        // SSN is required only for United States residents (ISO2 "US").
        When(x => IsUnitedStates(x.Request.Country), () =>
        {
            RuleFor(x => x.Request.Ssn)
                .NotEmpty().WithMessage("SSN is required for United States residents.")
                .Matches(@"^\d{3}-\d{2}-\d{4}$").WithMessage("SSN must be in the format XXX-XX-XXXX.");

            // EIN is optional (only applies to ambassadors who operate as a business),
            // but when provided it must follow the IRS format XX-XXXXXXX.
            RuleFor(x => x.Request.Ein)
                .Matches(@"^\d{2}-\d{7}$").WithMessage("EIN must be in the format XX-XXXXXXX.")
                .When(x => !string.IsNullOrEmpty(x.Request.Ein));
        });
    }

    private static bool IsUnitedStates(string country)
        => !string.IsNullOrWhiteSpace(country) &&
           (country.Equals("US", StringComparison.OrdinalIgnoreCase) ||
            country.Equals("United States", StringComparison.OrdinalIgnoreCase) ||
            country.Equals("United States of America", StringComparison.OrdinalIgnoreCase));
}
