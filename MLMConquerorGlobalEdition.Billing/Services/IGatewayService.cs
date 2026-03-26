using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services;

public interface IGatewayService
{
    WalletType GatewayType { get; }

    Task<Result<string>> ChargeAsync(
        string memberId,
        decimal amount,
        string currency,
        string description,
        CancellationToken ct = default);

    Task<Result<bool>> RefundAsync(
        string gatewayTransactionId,
        decimal amount,
        CancellationToken ct = default);
}
