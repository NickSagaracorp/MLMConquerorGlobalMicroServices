using FluentValidation.TestHelper;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.UpdatePayoutGatewaySetting;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Payouts;

public class UpdatePayoutGatewaySettingCommandValidatorTests
{
    private readonly UpdatePayoutGatewaySettingCommandValidator _validator = new();

    private static UpdatePayoutGatewaySettingCommand Cmd(
        decimal min = 25m, decimal fee = 1.95m, decimal? minFee = null,
        string display = "eWallet", string currency = "USD")
        => new(WalletType.eWallet, display, fee, AdminFeeKind.Fixed, minFee, currency, min, IsActive: true);

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.TestValidate(Cmd());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_NegativeMinimum_Fails()
    {
        var result = _validator.TestValidate(Cmd(min: -1m));
        result.ShouldHaveValidationErrorFor(x => x.MinimumPayoutAmount);
    }

    [Fact]
    public void Validate_NegativeAdminFee_Fails()
    {
        var result = _validator.TestValidate(Cmd(fee: -0.01m));
        result.ShouldHaveValidationErrorFor(x => x.AdminFee);
    }

    [Fact]
    public void Validate_NegativeMinAdminFee_Fails()
    {
        var result = _validator.TestValidate(Cmd(minFee: -5m));
        result.ShouldHaveValidationErrorFor(x => x.MinAdminFee);
    }

    [Fact]
    public void Validate_EmptyDisplayName_Fails()
    {
        var result = _validator.TestValidate(Cmd(display: ""));
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void Validate_EmptyCurrency_Fails()
    {
        var result = _validator.TestValidate(Cmd(currency: ""));
        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }
}
