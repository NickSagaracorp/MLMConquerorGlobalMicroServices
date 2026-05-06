using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.SignupAPI.Services;

/// <summary>
/// Records FingerprintJS visitor events on the public join pages and decides whether
/// a request crosses the duplicate-attempt threshold (e.g. same browser ID seen ≥ N
/// times in the last X hours under different emails).
///
/// Threshold logic is server-side so an attacker can't bypass it by stripping the
/// fingerprint client-side — we still flag based on IP fallback when VisitorId is
/// missing, and we never trust the value beyond a non-empty string.
/// </summary>
public interface IFraudFingerprintService
{
    /// <summary>
    /// Persists a fingerprint event and returns whether it tripped the duplicate guard.
    /// When IsFlagged = true, the caller should reject the signup attempt with a generic
    /// error so the attacker doesn't learn what tripped them.
    /// </summary>
    Task<FingerprintCaptureResult> RecordAsync(
        string? visitorId,
        SignupRiskFlow flow,
        string? sponsorReplicateSite,
        string? ipAddress,
        string? userAgent,
        string? orderId,
        string? memberId,
        CancellationToken ct);
}

/// <summary>Outcome of a fingerprint capture call.</summary>
public record FingerprintCaptureResult(bool IsFlagged, string? FlagReason, long EventId);
