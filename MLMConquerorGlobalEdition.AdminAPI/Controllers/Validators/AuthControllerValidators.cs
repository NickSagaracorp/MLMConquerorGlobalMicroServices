using FluentValidation;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers.Validators;

/// <summary>
/// Validator for the nested AuthController.LoginRequest record.
/// </summary>
public class AdminLoginRequestValidator : AbstractValidator<AuthController.LoginRequest>
{
    public AdminLoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(AdminValidationPatterns.EmailMaxLength)
            .EmailAddress()
            .Matches(AdminValidationPatterns.EmailPattern)
                .WithMessage("Email contains invalid characters.");

        // Login itself only caps length — strength is enforced at password change.
        RuleFor(x => x.Password)
            .NotEmpty()
            .MaximumLength(AdminValidationPatterns.PasswordMaxLength);
    }
}
