using FluentValidation;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Validation;

namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Profile.Validators;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.Phone)
            .Matches(BizCenterValidationPatterns.PhonePattern)
                .WithMessage("Phone must be 7-20 chars (digits, spaces, dashes, parens, optional +).")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.WhatsApp)
            .Matches(BizCenterValidationPatterns.PhonePattern)
                .WithMessage("WhatsApp must be 7-20 chars (digits, spaces, dashes, parens, optional +).")
            .When(x => !string.IsNullOrEmpty(x.WhatsApp));

        RuleFor(x => x.Country)
            .Matches(BizCenterValidationPatterns.CountryIsoPattern)
                .WithMessage("Country must be a 2-letter ISO 3166-1 alpha-2 code (uppercase).")
            .When(x => !string.IsNullOrEmpty(x.Country));

        RuleFor(x => x.State)
            .MaximumLength(BizCenterValidationPatterns.StateMaxLength)
            .Matches(BizCenterValidationPatterns.SafeTextPattern)
            .When(x => !string.IsNullOrEmpty(x.State));

        RuleFor(x => x.City)
            .MaximumLength(BizCenterValidationPatterns.CityMaxLength)
            .Matches(BizCenterValidationPatterns.SafeTextPattern)
            .When(x => !string.IsNullOrEmpty(x.City));

        RuleFor(x => x.Address)
            .MaximumLength(BizCenterValidationPatterns.AddressMaxLength)
            .Matches(BizCenterValidationPatterns.SafeTextPattern)
            .When(x => !string.IsNullOrEmpty(x.Address));

        RuleFor(x => x.ZipCode)
            .MaximumLength(BizCenterValidationPatterns.ZipCodeMaxLength)
            .Matches(BizCenterValidationPatterns.SafeTextPattern)
            .When(x => !string.IsNullOrEmpty(x.ZipCode));

        RuleFor(x => x.AddressChangeReason)
            .MaximumLength(BizCenterValidationPatterns.ReasonMaxLength)
            .Matches(BizCenterValidationPatterns.SafeTextPattern)
            .When(x => !string.IsNullOrEmpty(x.AddressChangeReason));

        RuleFor(x => x.DefaultLanguage)
            .Matches(BizCenterValidationPatterns.LanguageTagPattern)
                .WithMessage("DefaultLanguage must be a BCP 47 tag like 'en' or 'en-US'.")
            .When(x => !string.IsNullOrEmpty(x.DefaultLanguage));

        RuleFor(x => x.PayoutFrequency)
            .Matches(BizCenterValidationPatterns.PayoutFrequencyPattern)
                .WithMessage("PayoutFrequency must be 'Daily', 'Weekly' or 'Monthly'.")
            .When(x => !string.IsNullOrEmpty(x.PayoutFrequency));
    }
}

public class UpdateReplicateSiteRequestValidator : AbstractValidator<UpdateReplicateSiteRequest>
{
    public UpdateReplicateSiteRequestValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(BizCenterValidationPatterns.ReplicateSlugMaxLength)
            .Matches(BizCenterValidationPatterns.ReplicateSlugPattern)
                .WithMessage("Slug must be lowercase alphanumeric with hyphens (2-50 chars), no leading or trailing hyphen.");
    }
}

public class UpdateEmailRequestValidator : AbstractValidator<UpdateEmailRequest>
{
    public UpdateEmailRequestValidator()
    {
        RuleFor(x => x.NewEmail)
            .NotEmpty()
            .MaximumLength(BizCenterValidationPatterns.EmailMaxLength)
            .EmailAddress()
            .Matches(BizCenterValidationPatterns.EmailPattern)
                .WithMessage("Email contains invalid characters.");
    }
}

public class UpdatePasswordRequestValidator : AbstractValidator<UpdatePasswordRequest>
{
    public UpdatePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .MaximumLength(BizCenterValidationPatterns.PasswordMaxLength);

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(BizCenterValidationPatterns.PasswordMinLength)
            .MaximumLength(BizCenterValidationPatterns.PasswordMaxLength)
            .Matches(BizCenterValidationPatterns.PasswordPattern)
                .WithMessage("Password must contain at least one digit, one uppercase letter, one lowercase letter and one special character.");
    }
}

public class UpdatePhotoRequestValidator : AbstractValidator<UpdatePhotoRequest>
{
    public UpdatePhotoRequestValidator()
    {
        RuleFor(x => x.ContentType)
            .NotEmpty()
            .MaximumLength(50)
            .Matches(BizCenterValidationPatterns.ImageContentTypePattern)
                .WithMessage("ContentType must be image/png, image/jpeg, image/webp or image/gif.");

        RuleFor(x => x.Base64Image)
            .NotEmpty()
            .MaximumLength(BizCenterValidationPatterns.Base64MaxLength)
                .WithMessage("Photo payload exceeds the maximum allowed size.")
            .Matches(BizCenterValidationPatterns.Base64Pattern)
                .WithMessage("Photo must be valid base64 (with optional data: prefix).");
    }
}
