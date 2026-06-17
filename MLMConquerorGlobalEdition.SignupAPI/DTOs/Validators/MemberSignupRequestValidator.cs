using FluentValidation;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Validators;

public class MemberSignupRequestValidator : AbstractValidator<MemberSignupRequest>
{
    public MemberSignupRequestValidator()
    {
        RuleFor(x => x.SponsorReplicateSite)
            .MaximumLength(64)
            .Must(value =>
                System.Text.RegularExpressions.Regex.IsMatch(value!, ValidationPatterns.MemberIdPattern) ||
                System.Text.RegularExpressions.Regex.IsMatch(value!, ValidationPatterns.ReplicateSlugPattern))
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

        RuleFor(x => x.Phone)
            .Matches(ValidationPatterns.PhonePattern)
                .WithMessage("Phone must contain digits, spaces, dashes and parentheses (7-20 chars).")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        // SSN required when Country == US
        When(x => string.Equals(x.Country, "US", StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.Ssn)
                .NotEmpty().WithMessage("SSN is required for US residents.")
                .Matches(ValidationPatterns.SsnPattern)
                    .WithMessage("SSN must be in the format XXX-XX-XXXX.");
        });

        RuleFor(x => x.MembershipLevelId)
            .GreaterThan(0);

        RuleFor(x => x.VisitorId)
            .MaximumLength(ValidationPatterns.VisitorIdMaxLength)
            .Matches(ValidationPatterns.VisitorIdPattern)
                .WithMessage("VisitorId must be 8-64 alphanumeric characters.")
            .When(x => !string.IsNullOrEmpty(x.VisitorId));
    }
}
