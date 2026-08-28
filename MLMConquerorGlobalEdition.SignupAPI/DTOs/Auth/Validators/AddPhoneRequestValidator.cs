using FluentValidation;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth.Validators;

public class AddPhoneRequestValidator : AbstractValidator<AddPhoneRequest>
{
    public AddPhoneRequestValidator()
    {
        // La misma regla que aplica el transporte de SMS, invocada, no copiada: una segunda
        // versión del patrón acabaría aceptando aquí números que fallan al enviarse.
        // ValidationPatterns.PhonePattern no sirve — admite espacios, guiones y paréntesis, que
        // E.164 no permite.
        RuleFor(x => x.PhoneE164)
            .NotEmpty()
            .MaximumLength(16)
            .Must(PhoneNumberFormat.IsE164)
                .WithMessage("Phone must be in E.164 format: '+' followed by 8 to 15 digits, no separators.");
    }
}
