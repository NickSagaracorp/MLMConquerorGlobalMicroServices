using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payouts;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutGatewaySettings;

/// <summary>
/// Lists every payout gateway from the single PaymentGatewayInfo catalog (active + inactive),
/// so admins can manage fees, the minimum payout threshold and the active flag in one place.
/// </summary>
public class GetPayoutGatewaySettingsHandler
    : IRequestHandler<GetPayoutGatewaySettingsQuery, Result<List<PayoutGatewayDto>>>
{
    private readonly AppDbContext _db;
    public GetPayoutGatewaySettingsHandler(AppDbContext db) => _db = db;

    public async Task<Result<List<PayoutGatewayDto>>> Handle(
        GetPayoutGatewaySettingsQuery request, CancellationToken ct)
    {
        var items = await _db.PaymentGateways
            .AsNoTracking()
            .OrderBy(g => g.WalletType)
            .Select(g => new PayoutGatewayDto
            {
                Id = g.Id,
                WalletType = g.WalletType,
                DisplayName = g.DisplayName,
                Description = g.Description,
                AdminFee = g.AdminFee,
                AdminFeeKind = g.AdminFeeKind,
                MinAdminFee = g.MinAdminFee,
                Currency = g.Currency,
                MinimumPayoutAmount = g.MinimumPayoutAmount,
                IsActive = g.IsActive
            })
            .ToListAsync(ct);

        return Result<List<PayoutGatewayDto>>.Success(items);
    }
}
