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

        // Los selectores se validan contra listas cerradas: si acá entra un valor libre,
        // PayQuickerSettingsProvider no encuentra la ApiCredential y el gateway queda mudo
        // hasta que alguien revise. Mejor rechazarlo al guardar.
        RuleFor(x => x.ApiVersion)
            .Must(v => v is "V1" or "V2")
            .WithMessage("ApiVersion must be 'V1' or 'V2'.")
            .When(x => !string.IsNullOrWhiteSpace(x.ApiVersion));

        RuleFor(x => x.Environment)
            .Must(v => v is "Sandbox" or "Production" or "Test")
            .WithMessage("Environment must be 'Sandbox', 'Production' or 'Test'.")
            .When(x => !string.IsNullOrWhiteSpace(x.Environment));

        RuleFor(x => x.AdminPortalUrl)
            .MaximumLength(500)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var parsed)
                         && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
            .WithMessage("Admin portal URL must be an absolute http(s) URL.")
            .When(x => !string.IsNullOrWhiteSpace(x.AdminPortalUrl));
    }
}
