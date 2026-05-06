using FluentValidation;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tokens.TransferTokenInstance;

public class TransferTokenInstanceValidator : AbstractValidator<TransferTokenInstanceCommand>
{
    public TransferTokenInstanceValidator()
    {
        RuleFor(x => x.TokenCode)
            .NotEmpty().WithMessage("Token code is required.")
            .MaximumLength(20);

        RuleFor(x => x.RecipientMemberId)
            .NotEmpty().WithMessage("Recipient is required.")
            .MaximumLength(36);

        RuleFor(x => x.Notes)
            .MaximumLength(500);
    }
}
