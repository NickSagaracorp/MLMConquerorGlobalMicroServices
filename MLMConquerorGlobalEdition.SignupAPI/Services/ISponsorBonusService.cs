namespace MLMConquerorGlobalEdition.SignupAPI.Services;

public interface ISponsorBonusService
{
    /// <summary>
    /// Creates a sponsor bonus CommissionEarning for the sponsor of a newly completed signup.
    /// Idempotent — silently skips if already recorded for the same order.
    /// Does NOT call SaveChangesAsync; the caller is responsible for persisting.
    /// </summary>
    Task ComputeAsync(
        string? sponsorMemberId,
        string newMemberId,
        string orderId,
        decimal orderTotal,
        string createdBy,
        DateTime now,
        CancellationToken ct);

    /// <summary>
    /// Reverses the sponsor bonus for a signup order when the cancellation falls within
    /// the 14-day chargeback window.
    /// - Pending commission → cancelled in place.
    /// - Paid commission    → new negative-amount CommissionEarning using CommissionType.ReverseId.
    /// Does NOT call SaveChangesAsync; the caller is responsible for persisting.
    /// </summary>
    Task TryReverseAsync(
        string cancelledMemberId,
        string signupOrderId,
        string? reason,
        DateTime now,
        string actorId,
        CancellationToken ct);
}
