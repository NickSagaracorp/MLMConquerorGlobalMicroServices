using FluentValidation;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporateEvents.Validators;

public class CreateCorporateEventRequestValidator : AbstractValidator<CreateCorporateEventRequest>
{
    public CreateCorporateEventRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(AdminValidationPatterns.SubjectMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern)
                .WithMessage("Title contains invalid characters.");

        RuleFor(x => x.Description)
            .MaximumLength(AdminValidationPatterns.LongTextMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern)
                .WithMessage("Description contains invalid characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.EventDate)
            .NotEmpty();

        RuleFor(x => x.Location)
            .MaximumLength(AdminValidationPatterns.LocationMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern)
                .WithMessage("Location contains invalid characters.")
            .When(x => !string.IsNullOrEmpty(x.Location));

        RuleFor(x => x.ImageUrl)
            .MaximumLength(AdminValidationPatterns.UrlMaxLength)
            .Matches(AdminValidationPatterns.UrlPattern)
                .WithMessage("ImageUrl must be a valid http/https URL.")
            .When(x => !string.IsNullOrEmpty(x.ImageUrl));
    }
}

public class UpdateCorporateEventRequestValidator : AbstractValidator<UpdateCorporateEventRequest>
{
    public UpdateCorporateEventRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(AdminValidationPatterns.SubjectMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern);

        RuleFor(x => x.Description)
            .MaximumLength(AdminValidationPatterns.LongTextMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern)
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.EventDate).NotEmpty();

        RuleFor(x => x.Location)
            .MaximumLength(AdminValidationPatterns.LocationMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern)
            .When(x => !string.IsNullOrEmpty(x.Location));

        RuleFor(x => x.ImageUrl)
            .MaximumLength(AdminValidationPatterns.UrlMaxLength)
            .Matches(AdminValidationPatterns.UrlPattern)
            .When(x => !string.IsNullOrEmpty(x.ImageUrl));
    }
}
