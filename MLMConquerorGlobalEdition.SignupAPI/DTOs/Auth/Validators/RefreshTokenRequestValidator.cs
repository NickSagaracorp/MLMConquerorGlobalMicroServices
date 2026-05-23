using FluentValidation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth.Validators;

public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            // Refresh tokens are opaque base64url/JWT-shaped strings — cap to
            // 4 KB to reject oversize payloads without locking in a format.
            .MaximumLength(4096)
            .Matches(@"^[A-Za-z0-9_\-\.=]+$")
                .WithMessage("Refresh token contains invalid characters.");
    }
}
