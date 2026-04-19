namespace MLMConquerorGlobalEdition.SignupAPI.Services;

public interface IFastStartBonusService
{
    /// <summary>
    /// Checks whether a newly completed signup triggers a Fast Start Bonus for the sponsor.
    /// Only Elite (3) and Turbo (4) membership levels count. FSB fires when the 2nd qualifying
    /// sponsored member joins within the active window. Idempotent — skips if already recorded.
    /// Does NOT call SaveChangesAsync; the caller is responsible for persisting.
    /// </summary>
    Task ComputeAsync(
        string? sponsorMemberId,
        string newMemberId,
        string orderId,
        DateTime now,
        string createdBy,
        CancellationToken ct);
}
