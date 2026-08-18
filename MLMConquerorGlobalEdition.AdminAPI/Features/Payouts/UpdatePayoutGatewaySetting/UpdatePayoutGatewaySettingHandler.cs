using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.UpdatePayoutGatewaySetting;

/// <summary>
/// Updates the single PaymentGatewayInfo row for a WalletType. The catalog is a fixed set
/// (one row per supported gateway), so this is an update — not an upsert.
/// </summary>
public class UpdatePayoutGatewaySettingHandler
    : IRequestHandler<UpdatePayoutGatewaySettingCommand, Result<PayoutGatewayDto>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICurrentUserService _currentUser;

    public UpdatePayoutGatewaySettingHandler(
        AppDbContext db, IDateTimeProvider dateTime, ICurrentUserService currentUser)
    {
        _db = db;
        _dateTime = dateTime;
        _currentUser = currentUser;
    }

    public async Task<Result<PayoutGatewayDto>> Handle(
        UpdatePayoutGatewaySettingCommand request, CancellationToken ct)
    {
        // Input validation (non-negative amounts, required fields) runs in the
        // ValidationBehavior pipeline via UpdatePayoutGatewaySettingCommandValidator.
        var gateway = await _db.PaymentGateways
            .FirstOrDefaultAsync(g => g.WalletType == request.WalletType, ct);

        if (gateway is null)
            return Result<PayoutGatewayDto>.Failure("GATEWAY_NOT_FOUND",
                $"No payout gateway exists for wallet type '{request.WalletType}'.");

        gateway.DisplayName = request.DisplayName;
        gateway.AdminFee = request.AdminFee;
        gateway.AdminFeeKind = request.AdminFeeKind;
        gateway.MinAdminFee = request.MinAdminFee;
        gateway.Currency = request.Currency;
        gateway.MinimumPayoutAmount = request.MinimumPayoutAmount;
        gateway.IsActive = request.IsActive;
        // Normalizados a null cuando vienen vacíos: un string vacío en ApiVersion haría que
        // PayQuickerSettingsProvider busque la ServiceKey "PayQuicker" y no encuentre nada.
        gateway.ApiVersion = string.IsNullOrWhiteSpace(request.ApiVersion) ? null : request.ApiVersion!.Trim();
        gateway.Environment = string.IsNullOrWhiteSpace(request.Environment) ? null : request.Environment!.Trim();
        gateway.AdminPortalUrl = string.IsNullOrWhiteSpace(request.AdminPortalUrl) ? null : request.AdminPortalUrl!.Trim();
        gateway.LastUpdateDate = _dateTime.Now;
        gateway.LastUpdateBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        return Result<PayoutGatewayDto>.Success(new PayoutGatewayDto
        {
            Id = gateway.Id,
            WalletType = gateway.WalletType,
            DisplayName = gateway.DisplayName,
            Description = gateway.Description,
            AdminFee = gateway.AdminFee,
            AdminFeeKind = gateway.AdminFeeKind,
            MinAdminFee = gateway.MinAdminFee,
            Currency = gateway.Currency,
            MinimumPayoutAmount = gateway.MinimumPayoutAmount,
            IsActive = gateway.IsActive,
            ApiVersion = gateway.ApiVersion,
            Environment = gateway.Environment,
            AdminPortalUrl = gateway.AdminPortalUrl
        });
    }
}
