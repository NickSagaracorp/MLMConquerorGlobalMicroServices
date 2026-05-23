using FluentValidation;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Validation;

namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Wallet.Validators;

public class UpdateWalletRequestValidator : AbstractValidator<UpdateWalletRequest>
{
    public UpdateWalletRequestValidator()
    {
        RuleFor(x => x.WalletType).IsInEnum();

        RuleFor(x => x.AccountIdentifier)
            .MaximumLength(200)
            .Matches(BizCenterValidationPatterns.AccountIdentifierPattern)
                .WithMessage("AccountIdentifier contains invalid characters.")
            .When(x => !string.IsNullOrEmpty(x.AccountIdentifier));
    }
}
