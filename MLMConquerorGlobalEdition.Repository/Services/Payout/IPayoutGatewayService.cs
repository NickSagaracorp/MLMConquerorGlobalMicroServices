using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Payout;

/// <summary>
/// Outbound payout gateway (pays ambassadors). Separate from IGatewayService (which charges
/// members) to respect ISP. One implementation per WalletType, resolved by IPayoutGatewayResolver.
/// </summary>
public interface IPayoutGatewayService
{
    WalletType GatewayType { get; }

    Task<Result<PayoutAccountResult>>  SubscribeAccountAsync(PayoutAccountContext ctx, CancellationToken ct = default);
    Task<Result<PayoutAccountResult>>  ValidateAccountAsync(PayoutAccountContext ctx, CancellationToken ct = default);
    Task<Result<PayoutBalanceResult>>  GetBalanceAsync(string memberId, string accountIdentifier, CancellationToken ct = default);
    Task<Result<PayoutTransferResult>> DisburseAsync(PayoutTransferContext ctx, CancellationToken ct = default);

    /// <summary>
    /// Looks up the authoritative state of a transfer by its reference (the PayoutAttempt.Id).
    /// Used by the reconciliation sweep to resolve attempts stuck Pending after a crash between
    /// disburse and the local SaveChanges: it answers "did the money actually leave?" so the
    /// sweep can either finalize (money sent) or release the earnings (money not sent) — never guess.
    /// </summary>
    Task<Result<PayoutTransferStatusResult>> GetTransferStatusAsync(string reference, CancellationToken ct = default);
}

public class PayoutAccountContext
{
    public string MemberId { get; init; } = string.Empty;
    public WalletType WalletType { get; init; }
    public string AccountIdentifier { get; init; } = string.Empty;
    public string? AccountMeta { get; init; }
}

public class PayoutTransferContext
{
    public string MemberId { get; init; } = string.Empty;
    public WalletType WalletType { get; init; }
    public string AccountIdentifier { get; init; } = string.Empty;
    public decimal AmountUsd { get; init; }
    public string Reference { get; init; } = string.Empty; // PayoutAttempt.Id as string
}

public class PayoutAccountResult
{
    public bool Exists { get; init; }
    public string? GatewayCode { get; init; }
    public string? GatewayMessage { get; init; }
}

public class PayoutBalanceResult
{
    public decimal Balance { get; init; }
    public string Currency { get; init; } = "USD";
    public string? GatewayCode { get; init; }
    public string? GatewayMessage { get; init; }
}

public class PayoutTransferResult
{
    public string GatewayTransactionId { get; init; } = string.Empty;
    public string? GatewayCode { get; init; }
    public string? GatewayMessage { get; init; }
}

/// <summary>Authoritative state of a previously-submitted transfer, as reported by the gateway.</summary>
public enum PayoutTransferState
{
    /// <summary>The transfer completed — money left. The attempt must be finalized as Success.</summary>
    Succeeded,
    /// <summary>The gateway processed and rejected the transfer — no money left. Earnings can be released.</summary>
    Failed,
    /// <summary>The gateway has no record of this reference — the disburse never reached it. Earnings can be released.</summary>
    NotFound,
    /// <summary>The gateway could not be reached or returned an indeterminate state. Leave Pending; retry next sweep.</summary>
    Unknown
}

public class PayoutTransferStatusResult
{
    public PayoutTransferState State { get; init; }
    public string? GatewayTransactionId { get; init; }
    public string? GatewayCode { get; init; }
    public string? GatewayMessage { get; init; }
}
