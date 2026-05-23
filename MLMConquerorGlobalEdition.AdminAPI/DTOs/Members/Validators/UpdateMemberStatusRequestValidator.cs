using FluentValidation;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Members.Validators;

public class UpdateMemberStatusRequestValidator : AbstractValidator<UpdateMemberStatusRequest>
{
    public UpdateMemberStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();

        RuleFor(x => x.Reason)
            .MaximumLength(AdminValidationPatterns.NotesMaxLength)
            .Matches(AdminValidationPatterns.SafeTextPattern)
                .WithMessage("Reason contains invalid characters.")
            .When(x => !string.IsNullOrEmpty(x.Reason));
    }
}
