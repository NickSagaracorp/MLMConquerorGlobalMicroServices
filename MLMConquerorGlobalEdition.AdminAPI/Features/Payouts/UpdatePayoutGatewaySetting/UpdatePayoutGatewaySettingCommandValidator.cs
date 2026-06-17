using FluentValidation;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.UpdatePayoutGatewaySetting;

/// <summary>
/// Input validation for updating a payout gateway. Runs in the MediatR ValidationBehavior
/// pipeline before the handler. Business lookups (e.g. gateway-not-found) stay in the handler.
/// </summary>
public class UpdatePayoutGatewaySettingCommandValidator : AbstractValidator<UpdatePayoutGatewaySettingCommand>
{
    public UpdatePayoutGatewaySettingCommandValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .MaximumLength(150);

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .MaximumLength(10);

        RuleFor(x => x.MinimumPayoutAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum payout amount cannot be negative.");

        RuleFor(x => x.AdminFee)
            .GreaterThanOrEqualTo(0).WithMessage("Admin fee cannot be negative.");

        RuleFor(x => x.MinAdminFee)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum admin fee cannot be negative.")
            .When(x => x.MinAdminFee.HasValue);
    }
}
