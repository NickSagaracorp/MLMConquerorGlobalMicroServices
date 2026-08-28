using FluentValidation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth.Validators;

public class VerifyPhoneRequestValidator : AbstractValidator<VerifyPhoneRequest>
{
    public VerifyPhoneRequestValidator()
    {
        RuleFor(x => x.ChallengeToken)
            .NotEmpty()
            .MaximumLength(4096)
            .Matches(@"^[A-Za-z0-9_\-\.=]+$")
                .WithMessage("Challenge token contains invalid characters.");

        // 2FA code — exactly 6 digits, el formato que emite la librería Authn
        RuleFor(x => x.Code)
            .NotEmpty()
            .Matches(@"^\d{6}$")
                .WithMessage("Verification code must be exactly 6 digits.");
    }
}
