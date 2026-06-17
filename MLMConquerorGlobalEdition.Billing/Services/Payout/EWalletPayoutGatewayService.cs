using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout;

/// <summary>
/// i-payout (eWallet) payout gateway. Runs in simulated mode until real credentials are
/// wired (same approach as SpreedlyCardGatewayService). Real HTTP calls go behind the
/// ApiCredential lookup in a later sprint. An account identifier containing "FAIL"
/// simulates a gateway rejection, for manual negative testing.
/// </summary>
public class EWalletPayoutGatewayService : IPayoutGatewayService
{
    public WalletType GatewayType => WalletType.eWallet;

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
            new PayoutTransferResult { GatewayTransactionId = $"sim-ewallet-{Guid.NewGuid():N}", GatewayCode = "SIM_OK" }));
    }

    // Simulated: stubs don't persist transfers, so a transfer that was attempted is assumed to
    // have completed (matching DisburseAsync's success path). The real i-payout integration will
    // query the provider by reference. A reference containing "FAIL" reports NotFound for testing.
    public Task<Result<PayoutTransferStatusResult>> GetTransferStatusAsync(string reference, CancellationToken ct = default)
    {
        if (reference.Contains("FAIL", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(Result<PayoutTransferStatusResult>.Success(
                new PayoutTransferStatusResult { State = PayoutTransferState.NotFound, GatewayCode = "SIM_NOT_FOUND" }));

        return Task.FromResult(Result<PayoutTransferStatusResult>.Success(
            new PayoutTransferStatusResult
            {
                State = PayoutTransferState.Succeeded,
                GatewayTransactionId = $"sim-ewallet-recon-{reference}",
                GatewayCode = "SIM_OK"
            }));
    }
}
