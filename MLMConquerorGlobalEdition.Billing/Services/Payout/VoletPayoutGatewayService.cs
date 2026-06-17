using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout;

/// <summary>
/// Volet payout gateway. Simulated mode until real credentials are wired.
/// </summary>
public class VoletPayoutGatewayService : IPayoutGatewayService
{
    public WalletType GatewayType => WalletType.Volet;

    public Task<Result<PayoutAccountResult>> SubscribeAccountAsync(PayoutAccountContext ctx, CancellationToken ct = default)
        => Task.FromResult(Result<PayoutAccountResult>.Success(
            new PayoutAccountResult { Exists = true, GatewayCode = "SIM_OK", GatewayMessage = "Simulated subscribe" }));

    public Task<Result<PayoutAccountResult>> ValidateAccountAsync(PayoutAccountContext ctx, CancellationToken ct = default)
        => Task.FromResult(Result<PayoutAccountResult>.Success(
            new PayoutAccountResult { Exists = true, GatewayCode = "SIM_OK", GatewayMessage = "Simulated validate" }));

    public Task<Result<PayoutBalanceResult>> GetBalanceAsync(string memberId, string accountIdentifier, CancellationToken ct = default)
        => Task.FromResult(Result<PayoutBalanceResult>.Success(
            new PayoutBalanceResult { Balance = 0m, Currency = "USD", GatewayCode = "SIM_OK", GatewayMessage = "Simulated balance" }));

    public Task<Result<PayoutTransferResult>> DisburseAsync(PayoutTransferContext ctx, CancellationToken ct = default)
    {
        if (ctx.AccountIdentifier.Contains("FAIL", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(Result<PayoutTransferResult>.Failure("SIM_REJECTED", "Simulated gateway rejection"));

        return Task.FromResult(Result<PayoutTransferResult>.Success(
            new PayoutTransferResult { GatewayTransactionId = $"sim-volet-{Guid.NewGuid():N}", GatewayCode = "SIM_OK" }));
    }

    // Simulated: see EWalletPayoutGatewayService.GetTransferStatusAsync. A reference containing
    // "FAIL" reports NotFound; otherwise the transfer is assumed completed.
    public Task<Result<PayoutTransferStatusResult>> GetTransferStatusAsync(string reference, CancellationToken ct = default)
    {
        if (reference.Contains("FAIL", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(Result<PayoutTransferStatusResult>.Success(
                new PayoutTransferStatusResult { State = PayoutTransferState.NotFound, GatewayCode = "SIM_NOT_FOUND" }));

        return Task.FromResult(Result<PayoutTransferStatusResult>.Success(
            new PayoutTransferStatusResult
            {
                State = PayoutTransferState.Succeeded,
                GatewayTransactionId = $"sim-volet-recon-{reference}",
                GatewayCode = "SIM_OK"
            }));
    }
}
