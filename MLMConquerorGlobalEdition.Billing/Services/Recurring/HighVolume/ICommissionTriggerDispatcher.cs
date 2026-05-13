namespace MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;

/// <summary>
/// Dispatch interface invoked by DownstreamTriggers (Stage 4) to enqueue commission
/// calculation jobs into the "commissions" Hangfire queue.
///
/// The Billing service only references this interface; the CommissionEngine provides
/// the concrete implementation. Hangfire serializes the invocation and the
/// CommissionEngine's worker picks it up from the "commissions" queue.
///
/// Supported TriggerTypes:
///   - "FastStartBonus": triggers FSB recalculation for the upline of the renewed member.
///   - "BoostBonus": triggers Boost Bonus evaluation for the renewed member's sponsor.
/// </summary>
public interface ICommissionTriggerDispatcher
{
    /// <summary>
    /// Dispatches a commission calculation for the given member and order.
    /// Invoked via Hangfire background job; must be idempotent.
    /// </summary>
    Task DispatchAsync(
        string memberId,
        string orderId,
        string triggerType,
        CancellationToken ct = default);
}
