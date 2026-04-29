using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Wallets;

/// <summary>
/// Single source of truth for wallet management. Used by BizCenter (member self-service)
/// and AdminAPI (admin viewing/auditing a member's wallets). Every state-changing call
/// emits a row in MemberProfilesWalletHistory and MemberWalletApiLog so we have a
/// complete paper trail for fraud investigation and gateway support escalations.
/// </summary>
public interface IMemberWalletService
{
    /// <summary>All wallet accounts the member has across every gateway type.</summary>
    Task<List<WalletAccountView>> GetAccountsAsync(string memberId, CancellationToken ct = default);

    /// <summary>The member's current default (preferred) wallet, if any.</summary>
    Task<WalletAccountView?> GetDefaultAsync(string memberId, CancellationToken ct = default);

    /// <summary>
    /// Create or update the wallet of a given type. If the AccountIdentifier changes,
    /// the previous value is preserved in <see cref="WalletHistoryView"/>. Stub-calls
    /// the underlying gateway API and writes the request/response into the wallet API log.
    /// </summary>
    Task<Result<WalletAccountView>> SaveAccountAsync(
        string memberId,
        SaveWalletRequest request,
        string actorIdentifier,
        CancellationToken ct = default);

    /// <summary>
    /// Marks the given wallet type as the member's default. The previous default
    /// is unset; both transitions are written to history.
    /// </summary>
    Task<Result<WalletAccountView>> SetDefaultAsync(
        string memberId,
        Domain.Enums.WalletType walletType,
        string actorIdentifier,
        CancellationToken ct = default);

    Task<PagedResult<WalletHistoryView>> GetHistoryAsync(
        string memberId, int page = 1, int pageSize = 50, CancellationToken ct = default);

    Task<PagedResult<WalletApiLogView>> GetApiLogsAsync(
        string memberId,
        Domain.Enums.WalletType? walletType = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default);

    /// <summary>Returns the configured gateways with their description, admin fee, etc.</summary>
    Task<List<PaymentGatewayInfoView>> GetGatewayInfoAsync(CancellationToken ct = default);
}
