using FluentValidation;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Ranks.Validators;

public class CreateRankDefinitionDtoValidator : AbstractValidator<CreateRankDefinitionDto>
{
    public CreateRankDefinitionDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty()
            .MaximumLength(AdminValidationPatterns.NameMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern);

        RuleFor(x => x.Description)
            .MaximumLength(AdminValidationPatterns.DescriptionMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern)
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);

        RuleFor(x => x.CertificateTemplateUrl)
            .MaximumLength(AdminValidationPatterns.UrlMaxLength)
            .Matches(AdminValidationPatterns.UrlPattern)
                .WithMessage("CertificateTemplateUrl must be a valid http/https URL.")
            .When(x => !string.IsNullOrEmpty(x.CertificateTemplateUrl));
    }
}

public class UpdateRankDefinitionDtoValidator : AbstractValidator<UpdateRankDefinitionDto>
{
    public UpdateRankDefinitionDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty()
            .MaximumLength(AdminValidationPatterns.NameMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern);

        RuleFor(x => x.Description)
            .MaximumLength(AdminValidationPatterns.DescriptionMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern)
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);

        RuleFor(x => x.Status)
            .NotEmpty()
            .Matches(AdminValidationPatterns.RankStatusPattern)
                .WithMessage("Status must be Active, Inactive or Archived.");

        RuleFor(x => x.CertificateTemplateUrl)
            .MaximumLength(AdminValidationPatterns.UrlMaxLength)
            .Matches(AdminValidationPatterns.UrlPattern)
            .When(x => !string.IsNullOrEmpty(x.CertificateTemplateUrl));
    }
}

public class CreateRankRequirementDtoValidator : AbstractValidator<CreateRankRequirementDto>
{
    public CreateRankRequirementDtoValidator()
    {
        RuleFor(x => x.RankDefinitionId).GreaterThan(0);
        RuleFor(x => x.LevelNo).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PersonalPoints).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TeamPoints).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxTeamPointsPerBranch).InclusiveBetween(0d, 1d);
        RuleFor(x => x.EnrollmentTeam).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PlacementQualifiedTeamMembers).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EnrollmentQualifiedTeamMembers).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxEnrollmentTeamPointsPerBranch).InclusiveBetween(0d, 1d);
        RuleFor(x => x.ExternalMembers).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SponsoredMembers).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SalesVolume).InclusiveBetween(0m, AdminValidationPatterns.AmountMax);
        RuleFor(x => x.RankBonus).InclusiveBetween(0m, AdminValidationPatterns.AmountMax);
        RuleFor(x => x.DailyBonus).InclusiveBetween(0m, AdminValidationPatterns.AmountMax);
        RuleFor(x => x.MonthlyBonus).InclusiveBetween(0m, AdminValidationPatterns.AmountMax);
        RuleFor(x => x.LifetimeHoldingDuration).GreaterThanOrEqualTo(0);

        RuleFor(x => x.RankDescription)
            .NotNull()
            .MaximumLength(AdminValidationPatterns.LongTextMaxLength);

        RuleFor(x => x.CurrentRankDescription)
            .NotNull()
            .MaximumLength(AdminValidationPatterns.LongTextMaxLength);

        RuleFor(x => x.AchievementMessage)
            .MaximumLength(AdminValidationPatterns.LongTextMaxLength)
            .When(x => !string.IsNullOrEmpty(x.AchievementMessage));

        RuleFor(x => x.CertificateUrl)
            .MaximumLength(AdminValidationPatterns.UrlMaxLength)
            .Matches(AdminValidationPatterns.UrlPattern)
            .When(x => !string.IsNullOrEmpty(x.CertificateUrl));
    }
}
