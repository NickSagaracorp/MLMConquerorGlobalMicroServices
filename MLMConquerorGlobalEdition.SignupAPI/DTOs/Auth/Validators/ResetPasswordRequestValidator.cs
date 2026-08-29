using FluentValidation;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth.Validators;

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        // Hace falta uno de los dos identificadores, no los dos: el componente de
        // SharedComponents postea UserId y la pantalla de BizCenterWeb postea Email. Exigir
        // Email como antes dejaría fuera al enlace del correo, que ahora lleva userId.
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.UserId) || !string.IsNullOrWhiteSpace(x.Email))
                .WithName("UserId")
                .WithMessage("Either UserId or Email is required.");

        // Los identificadores de Identity son GUID en texto; el tope y la lista blanca cortan
        // cualquier intento de colar otra cosa antes de que el handler consulte el almacén.
        RuleFor(x => x.UserId)
            .MaximumLength(450)
            .Matches(@"^[A-Za-z0-9\-]+$")
                .WithMessage("UserId contains invalid characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.UserId));

        RuleFor(x => x.Email)
            .MaximumLength(ValidationPatterns.EmailMaxLength)
            .EmailAddress()
            .Matches(ValidationPatterns.EmailPattern)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Token)
            .NotEmpty()
            // Identity reset tokens are base64url-ish; cap aggressively.
            .MaximumLength(4096)
            .Matches(@"^[A-Za-z0-9_\-\.=+/%]+$")
                .WithMessage("Reset token contains invalid characters.");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(ValidationPatterns.PasswordMinLength)
            .MaximumLength(ValidationPatterns.PasswordMaxLength)
            .Matches(ValidationPatterns.PasswordPattern)
                .WithMessage("Password must contain at least one digit, one uppercase letter, one lowercase letter and one special character.");
    }
}
