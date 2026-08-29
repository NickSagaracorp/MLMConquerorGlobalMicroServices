using FluentValidation;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth.Validators;

/// <summary>
/// Solo comprueba que el valor pertenezca al enum. Que el canal tenga destino para <b>esta</b>
/// cuenta lo decide el handler, que es quien puede mirar el teléfono y el enrolamiento del
/// usuario: un validador de DTO no conoce al usuario.
/// </summary>
public class SetTwoFactorChannelRequestValidator : AbstractValidator<SetTwoFactorChannelRequest>
{
    public SetTwoFactorChannelRequestValidator()
    {
        RuleFor(x => x.Channel)
            .IsInEnum()
                .WithMessage("Channel must be one of Authenticator, Email or Sms.");
    }
}
