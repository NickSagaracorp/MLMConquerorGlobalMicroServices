using FluentValidation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth.Validators;

public class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator()
    {
        // Los Id de Identity son GUID en texto; 450 es el ancho de la columna en AspNetUsers.
        RuleFor(x => x.UserId)
            .NotEmpty()
            .MaximumLength(450);

        // base64url sin relleno: exactamente el alfabeto que produce
        // WebEncoders.Base64UrlEncode, así que un '+' o un '=' aquí significa que el enlace
        // se construyó mal o que alguien lo está manipulando.
        RuleFor(x => x.Token)
            .NotEmpty()
            .MaximumLength(4096)
            .Matches(@"^[A-Za-z0-9_-]+$")
                .WithMessage("Confirmation token contains invalid characters.");
    }
}
