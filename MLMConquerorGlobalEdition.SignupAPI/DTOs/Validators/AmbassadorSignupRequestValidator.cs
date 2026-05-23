using FluentValidation;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Validators;

public class AmbassadorSignupRequestValidator : AbstractValidator<AmbassadorSignupRequest>
{
    public AmbassadorSignupRequestValidator()
    {
        RuleFor(x => x.SponsorReplicateSite)
            .MaximumLength(ValidationPatterns.ReplicateSlugMaxLength)
            .Matches(ValidationPatterns.ReplicateSlugPattern)
                .WithMessage("Sponsor replicate site is malformed.")
            .When(x => !string.IsNullOrEmpty(x.SponsorReplicateSite));

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(ValidationPatterns.NameMaxLength)
            .Matches(ValidationPatterns.NamePattern)
                .WithMessage("First name contains invalid characters.");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(ValidationPatterns.NameMaxLength)
            .Matches(ValidationPatterns.NamePattern)
                .WithMessage("Last name contains invalid characters.");

        RuleFor(x => x.DateOfBirth)
            .NotEmpty()
            .Must(BeAValidAge)
                .WithMessage("Applicant must be between 18 and 120 years old.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(ValidationPatterns.EmailMaxLength)
            .EmailAddress()
            .Matches(ValidationPatterns.EmailPattern)
                .WithMessage("Email contains invalid characters.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(ValidationPatterns.PasswordMinLength)
            .MaximumLength(ValidationPatterns.PasswordMaxLength)
            .Matches(ValidationPatterns.PasswordPattern)
                .WithMessage("Password must contain at least one digit, one uppercase letter, one lowercase letter and one special character.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password)
                .WithMessage("Passwords do not match.");

        RuleFor(x => x.Phone)
            .Matches(ValidationPatterns.PhonePattern)
                .WithMessage("Phone must contain digits, spaces, dashes and parentheses (7-20 chars).")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.WhatsApp)
            .Matches(ValidationPatterns.PhonePattern)
                .WithMessage("WhatsApp must contain digits, spaces, dashes and parentheses (7-20 chars).")
            .When(x => !string.IsNullOrEmpty(x.WhatsApp));

        RuleFor(x => x.Country)
            .NotEmpty()
            .Matches(ValidationPatterns.CountryIsoPattern)
                .WithMessage("Country must be a 2-letter ISO 3166-1 alpha-2 code (uppercase).");

        RuleFor(x => x.State)
            .MaximumLength(ValidationPatterns.StateMaxLength)
            .Matches(ValidationPatterns.SafeTextPattern)
                .WithMessage("State contains invalid characters.")
            .When(x => !string.IsNullOrEmpty(x.State));

        RuleFor(x => x.City)
            .MaximumLength(ValidationPatterns.CityMaxLength)
            .Matches(ValidationPatterns.SafeTextPattern)
                .WithMessage("City contains invalid characters.")
            .When(x => !string.IsNullOrEmpty(x.City));

        RuleFor(x => x.Address)
            .MaximumLength(ValidationPatterns.AddressMaxLength)
            .Matches(ValidationPatterns.SafeTextPattern)
                .WithMessage("Address contains invalid characters.")
            .When(x => !string.IsNullOrEmpty(x.Address));

        RuleFor(x => x.ZipCode)
            .MaximumLength(ValidationPatterns.ZipCodeMaxLength)
            .Matches(ValidationPatterns.SafeTextPattern)
                .WithMessage("Zip/postal code contains invalid characters.")
            .When(x => !string.IsNullOrEmpty(x.ZipCode));

        // SSN required when Country == US
        When(x => string.Equals(x.Country, "US", StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.Ssn)
                .NotEmpty().WithMessage("SSN is required for US residents.")
                .Matches(ValidationPatterns.SsnPattern)
                    .WithMessage("SSN must be in the format XXX-XX-XXXX.");

            RuleFor(x => x.Ein)
                .Matches(ValidationPatterns.EinPattern)
                    .WithMessage("EIN must be in the format XX-XXXXXXX.")
                .When(x => !string.IsNullOrEmpty(x.Ein));
        });

        RuleFor(x => x.BusinessName)
            .MaximumLength(ValidationPatterns.BusinessNameMaxLength)
            .Matches(ValidationPatterns.SafeTextPattern)
                .WithMessage("Business name contains invalid characters.")
            .When(x => !string.IsNullOrEmpty(x.BusinessName));

        RuleFor(x => x.ReplicateSiteSlug)
            .MaximumLength(ValidationPatterns.ReplicateSlugMaxLength)
            .Matches(ValidationPatterns.ReplicateSlugPattern)
                .WithMessage("Replicate site slug must be lowercase alphanumeric with hyphens (2-50 chars), no leading or trailing hyphen.")
            .When(x => !string.IsNullOrEmpty(x.ReplicateSiteSlug));

        RuleFor(x => x.MembershipLevelId)
            .GreaterThan(0)
            .LessThanOrEqualTo(int.MaxValue);

        RuleFor(x => x.VisitorId)
            .MaximumLength(ValidationPatterns.VisitorIdMaxLength)
            .Matches(ValidationPatterns.VisitorIdPattern)
                .WithMessage("VisitorId must be 8-64 alphanumeric characters.")
            .When(x => !string.IsNullOrEmpty(x.VisitorId));
    }

    private static bool BeAValidAge(DateTime dob)
    {
        if (dob == default) return false;
        var today = DateTime.Today;
        if (dob > today) return false;
        var age = today.Year - dob.Year;
        if (dob.Date > today.AddYears(-age)) age--;
        return age >= 18 && age <= 120;
    }
}
