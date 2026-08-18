using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.UpdatePayoutGatewaySetting;

/// <summary>
/// Updates a payout gateway (the single PaymentGatewayInfo row for a WalletType):
/// display name, per-gateway admin fee, the minimum payout threshold and the active flag.
/// </summary>
public record UpdatePayoutGatewaySettingCommand(
    WalletType WalletType,
    string DisplayName,
    decimal AdminFee,
    AdminFeeKind AdminFeeKind,
    decimal? MinAdminFee,
    string Currency,
    decimal MinimumPayoutAmount,
    bool IsActive,
    string? ApiVersion = null,
    string? Environment = null,
    string? AdminPortalUrl = null)
    : IRequest<Result<PayoutGatewayDto>>;
