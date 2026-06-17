using FluentValidation;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos.Validators;

public class CreateCorporatePromoRequestValidator : AbstractValidator<CreateCorporatePromoRequest>
{
    public CreateCorporatePromoRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(AdminValidationPatterns.SubjectMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern);

        RuleFor(x => x.Description)
            .MaximumLength(AdminValidationPatterns.LongTextMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern)
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate)
            .NotEmpty()
            .GreaterThanOrEqualTo(x => x.StartDate)
                .WithMessage("EndDate must be on or after StartDate.");

        RuleFor(x => x.BannerUrl)
            .MaximumLength(AdminValidationPatterns.UrlMaxLength)
            .Matches(AdminValidationPatterns.UrlPattern)
                .WithMessage("BannerUrl must be a valid http/https URL.")
            .When(x => !string.IsNullOrEmpty(x.BannerUrl));

        RuleFor(x => x.SponsorBonusMultiplier)
            .InclusiveBetween(1, 5)
                .WithMessage("SponsorBonusMultiplier must be between 1 (no boost) and 5.");

        RuleFor(x => x.BuilderBonusMultiplier)
            .InclusiveBetween(1, 5)
                .WithMessage("BuilderBonusMultiplier must be between 1 (no boost) and 5.");
    }
}

public class UpdateCorporatePromoRequestValidator : AbstractValidator<UpdateCorporatePromoRequest>
{
    public UpdateCorporatePromoRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(AdminValidationPatterns.SubjectMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern);

        RuleFor(x => x.Description)
            .MaximumLength(AdminValidationPatterns.LongTextMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern)
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate)
            .NotEmpty()
            .GreaterThanOrEqualTo(x => x.StartDate)
                .WithMessage("EndDate must be on or after StartDate.");

        RuleFor(x => x.BannerUrl)
            .MaximumLength(AdminValidationPatterns.UrlMaxLength)
            .Matches(AdminValidationPatterns.UrlPattern)
            .When(x => !string.IsNullOrEmpty(x.BannerUrl));

        RuleFor(x => x.SponsorBonusMultiplier)
            .InclusiveBetween(1, 5)
                .WithMessage("SponsorBonusMultiplier must be between 1 (no boost) and 5.");

        RuleFor(x => x.BuilderBonusMultiplier)
            .InclusiveBetween(1, 5)
                .WithMessage("BuilderBonusMultiplier must be between 1 (no boost) and 5.");
    }
}

public class UpsertPromoProductCommissionRequestValidator : AbstractValidator<UpsertPromoProductCommissionRequest>
{
    public UpsertPromoProductCommissionRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty()
            .MaximumLength(64)
            .Matches(AdminValidationPatterns.ProductIdPattern)
                .WithMessage("ProductId must be a 36-character GUID-shaped identifier.");
    }
}
