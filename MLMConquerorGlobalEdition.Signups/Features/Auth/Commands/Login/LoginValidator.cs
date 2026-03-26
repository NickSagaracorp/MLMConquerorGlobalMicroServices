using FluentValidation;
using MLMConquerorGlobalEdition.Signups.DTOs.Auth;

namespace MLMConquerorGlobalEdition.Signups.Features.Auth.Commands.Login;

public class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
