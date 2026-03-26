using FluentValidation;

namespace MLMConquerorGlobalEdition.Signups.Features.Signups.Commands.SignupMember;

public class SignupMemberValidator : AbstractValidator<SignupMemberCommand>
{
    public SignupMemberValidator()
    {
        RuleFor(x => x.Request.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Request.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.Request.Country).NotEmpty().MaximumLength(100);
    }
}
