using FluentValidation;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions.Validators;

public class CreateCommissionCategoryDtoValidator : AbstractValidator<CreateCommissionCategoryDto>
{
    public CreateCommissionCategoryDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(AdminValidationPatterns.NameMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern)
                .WithMessage("Name contains invalid characters.");

        RuleFor(x => x.Description)
            .MaximumLength(AdminValidationPatterns.DescriptionMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern)
                .WithMessage("Description contains invalid characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}

public class UpdateCommissionCategoryDtoValidator : AbstractValidator<UpdateCommissionCategoryDto>
{
    public UpdateCommissionCategoryDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(AdminValidationPatterns.NameMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern);

        RuleFor(x => x.Description)
            .MaximumLength(AdminValidationPatterns.DescriptionMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern)
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}

public class CreateCommissionRequestValidator : AbstractValidator<CreateCommissionRequest>
{
    public CreateCommissionRequestValidator()
    {
        RuleFor(x => x.BeneficiaryMemberId)
            .NotEmpty()
            .MaximumLength(32)
            .Matches(AdminValidationPatterns.MemberIdPattern)
                .WithMessage("BeneficiaryMemberId must be a valid member identifier.");

        RuleFor(x => x.CommissionTypeId)
            .GreaterThan(0);

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(AdminValidationPatterns.AmountMax)
            .PrecisionScale(18, 2, true);

        RuleFor(x => x.Notes)
            .MaximumLength(AdminValidationPatterns.NotesMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern)
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}

public class PayCommissionsRequestValidator : AbstractValidator<PayCommissionsRequest>
{
    public PayCommissionsRequestValidator()
    {
        RuleFor(x => x.CommissionIds)
            .NotNull()
            .Must(ids => ids.Count > 0 && ids.Count <= 1000)
                .WithMessage("CommissionIds must contain between 1 and 1000 entries.");

        RuleForEach(x => x.CommissionIds)
            .NotEmpty()
            .MaximumLength(64)
            .Matches(AdminValidationPatterns.ProductIdPattern)
                .WithMessage("Commission IDs must be 36-character GUID-shaped identifiers.");
    }
}

public class CreateCommissionTypeDtoValidator : AbstractValidator<CreateCommissionTypeDto>
{
    public CreateCommissionTypeDtoValidator()
    {
        RuleFor(x => x.CommissionCategoryId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty()
            .MaximumLength(AdminValidationPatterns.NameMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern);
        RuleFor(x => x.Description)
            .MaximumLength(AdminValidationPatterns.DescriptionMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern)
            .When(x => !string.IsNullOrEmpty(x.Description));
        RuleFor(x => x.Percentage).InclusiveBetween(0m, 1000m);
        RuleFor(x => x.Amount).InclusiveBetween(0m, AdminValidationPatterns.AmountMax)
            .When(x => x.Amount.HasValue);
        RuleFor(x => x.AmountPromo).InclusiveBetween(0m, AdminValidationPatterns.AmountMax)
            .When(x => x.AmountPromo.HasValue);
        RuleFor(x => x.PaymentDelayDays).InclusiveBetween(0, 3650);
        RuleFor(x => x.TriggerOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.NewMembers).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DaysAfterJoining).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MembersRebill).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LifeTimeRank).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CurrentRank).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LevelNo).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ResidualOverCommissionType).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ResidualPercentage).InclusiveBetween(0d, 1000d);
        RuleFor(x => x.PersonalPoints).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TeamPoints).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxTeamPointsPerBranch).InclusiveBetween(0d, 1d);
        RuleFor(x => x.EnrollmentTeam).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxEnrollmentTeamPointsPerBranch).InclusiveBetween(0d, 1d);
        RuleFor(x => x.ExternalMembers).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SponsoredMembers).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReverseId).GreaterThanOrEqualTo(0);
    }
}

public class UpdateCommissionTypeDtoValidator : AbstractValidator<UpdateCommissionTypeDto>
{
    public UpdateCommissionTypeDtoValidator()
    {
        RuleFor(x => x.CommissionCategoryId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty()
            .MaximumLength(AdminValidationPatterns.NameMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern);
        RuleFor(x => x.Description)
            .MaximumLength(AdminValidationPatterns.DescriptionMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern)
            .When(x => !string.IsNullOrEmpty(x.Description));
        RuleFor(x => x.Percentage).InclusiveBetween(0m, 1000m);
        RuleFor(x => x.Amount).InclusiveBetween(0m, AdminValidationPatterns.AmountMax)
            .When(x => x.Amount.HasValue);
        RuleFor(x => x.AmountPromo).InclusiveBetween(0m, AdminValidationPatterns.AmountMax)
            .When(x => x.AmountPromo.HasValue);
        RuleFor(x => x.PaymentDelayDays).InclusiveBetween(0, 3650);
        RuleFor(x => x.TriggerOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.NewMembers).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DaysAfterJoining).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MembersRebill).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LifeTimeRank).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CurrentRank).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LevelNo).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ResidualOverCommissionType).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ResidualPercentage).InclusiveBetween(0d, 1000d);
        RuleFor(x => x.PersonalPoints).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TeamPoints).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxTeamPointsPerBranch).InclusiveBetween(0d, 1d);
        RuleFor(x => x.EnrollmentTeam).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxEnrollmentTeamPointsPerBranch).InclusiveBetween(0d, 1d);
        RuleFor(x => x.ExternalMembers).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SponsoredMembers).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReverseId).GreaterThanOrEqualTo(0);
    }
}
