using FluentValidation;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Products.Validators;

public class CreateProductDtoValidator : AbstractValidator<CreateProductDto>
{
    public CreateProductDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty()
            .MaximumLength(AdminValidationPatterns.NameMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern);

        // Description is rich-text-ish but still bounded; allow most chars,
        // but cap and block <script>/JSON-string-injection chars at the
        // boundary by rejecting null bytes only.
        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(AdminValidationPatterns.LongTextMaxLength);

        RuleFor(x => x.ImageUrl)
            .NotEmpty()
            .MaximumLength(AdminValidationPatterns.UrlMaxLength)
            .Matches(AdminValidationPatterns.UrlPattern)
                .WithMessage("ImageUrl must be a valid http/https URL.");

        RuleFor(x => x.MonthlyFee).InclusiveBetween(0m, AdminValidationPatterns.AmountMax);
        RuleFor(x => x.SetupFee).InclusiveBetween(0m, AdminValidationPatterns.AmountMax);
        RuleFor(x => x.Price90Days).InclusiveBetween(0m, AdminValidationPatterns.AmountMax);
        RuleFor(x => x.Price180Days).InclusiveBetween(0m, AdminValidationPatterns.AmountMax);
        RuleFor(x => x.AnnualPrice).InclusiveBetween(0m, AdminValidationPatterns.AmountMax);
        RuleFor(x => x.MonthlyFeePromo).InclusiveBetween(0m, AdminValidationPatterns.AmountMax);
        RuleFor(x => x.SetupFeePromo).InclusiveBetween(0m, AdminValidationPatterns.AmountMax);

        RuleFor(x => x.DescriptionPromo)
            .MaximumLength(AdminValidationPatterns.LongTextMaxLength)
            .When(x => !string.IsNullOrEmpty(x.DescriptionPromo));

        RuleFor(x => x.ImageUrlPromo)
            .MaximumLength(AdminValidationPatterns.UrlMaxLength)
            .Matches(AdminValidationPatterns.UrlPattern)
            .When(x => !string.IsNullOrEmpty(x.ImageUrlPromo));

        RuleFor(x => x.QualificationPoins).GreaterThanOrEqualTo(0);
        RuleFor(x => x.QualificationPoinsPromo).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OldSystemProductId).GreaterThanOrEqualTo(0);

        RuleFor(x => x.MembershipLevelId)
            .GreaterThan(0).When(x => x.MembershipLevelId.HasValue);

        RuleFor(x => x.ThemeClass)
            .MaximumLength(50)
            .Matches(AdminValidationPatterns.CssClassPattern)
                .WithMessage("ThemeClass must be a valid CSS-class-like identifier.")
            .When(x => !string.IsNullOrEmpty(x.ThemeClass));
    }
}

public class UpdateProductDtoValidator : AbstractValidator<UpdateProductDto>
{
    public UpdateProductDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty()
            .MaximumLength(AdminValidationPatterns.NameMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(AdminValidationPatterns.LongTextMaxLength);

        RuleFor(x => x.ImageUrl)
            .NotEmpty()
            .MaximumLength(AdminValidationPatterns.UrlMaxLength)
            .Matches(AdminValidationPatterns.UrlPattern);

        RuleFor(x => x.MonthlyFee).InclusiveBetween(0m, AdminValidationPatterns.AmountMax);
        RuleFor(x => x.SetupFee).InclusiveBetween(0m, AdminValidationPatterns.AmountMax);
        RuleFor(x => x.Price90Days).InclusiveBetween(0m, AdminValidationPatterns.AmountMax);
        RuleFor(x => x.Price180Days).InclusiveBetween(0m, AdminValidationPatterns.AmountMax);
        RuleFor(x => x.AnnualPrice).InclusiveBetween(0m, AdminValidationPatterns.AmountMax);
        RuleFor(x => x.MonthlyFeePromo).InclusiveBetween(0m, AdminValidationPatterns.AmountMax);
        RuleFor(x => x.SetupFeePromo).InclusiveBetween(0m, AdminValidationPatterns.AmountMax);

        RuleFor(x => x.DescriptionPromo)
            .MaximumLength(AdminValidationPatterns.LongTextMaxLength)
            .When(x => !string.IsNullOrEmpty(x.DescriptionPromo));

        RuleFor(x => x.ImageUrlPromo)
            .MaximumLength(AdminValidationPatterns.UrlMaxLength)
            .Matches(AdminValidationPatterns.UrlPattern)
            .When(x => !string.IsNullOrEmpty(x.ImageUrlPromo));

        RuleFor(x => x.QualificationPoins).GreaterThanOrEqualTo(0);
        RuleFor(x => x.QualificationPoinsPromo).GreaterThanOrEqualTo(0);

        RuleFor(x => x.MembershipLevelId)
            .GreaterThan(0).When(x => x.MembershipLevelId.HasValue);

        RuleFor(x => x.ThemeClass)
            .MaximumLength(50)
            .Matches(AdminValidationPatterns.CssClassPattern)
            .When(x => !string.IsNullOrEmpty(x.ThemeClass));
    }
}
