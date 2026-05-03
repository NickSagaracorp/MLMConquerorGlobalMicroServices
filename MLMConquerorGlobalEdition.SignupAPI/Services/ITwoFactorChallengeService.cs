using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.Services;

/// <summary>
/// Issues and validates short-lived JWT challenge tokens carrying a hashed
/// 6-digit one-time code. The challenge JWT is signed with the same RSA key
/// as access tokens but uses a distinct <c>purpose</c> claim
/// (<c>2fa-challenge</c>) so it cannot be presented as a Bearer token.
/// </summary>
public interface ITwoFactorChallengeService
{
    /// <summary>Generates a fresh 6-digit numeric code (cryptographically random).</summary>
    string GenerateCode();

    /// <summary>SHA-256 hash of the code as a base64 string.</summary>
    string HashCode(string code);

    /// <summary>Mask the local-part of the email for UX (j***@example.com).</summary>
    string MaskEmail(string email);

    /// <summary>Default lifetime of a freshly issued challenge.</summary>
    TimeSpan ChallengeLifetime { get; }

    /// <summary>Maximum age of a token still accepted for resend (signature still valid even past <see cref="ChallengeLifetime"/>).</summary>
    TimeSpan ResendGraceWindow { get; }

    /// <summary>Issues a new challenge JWT bound to the given user, email, and code hash.</summary>
    string IssueChallenge(string userId, string email, string codeHash);

    /// <summary>
    /// Validates a challenge JWT. When <paramref name="allowExpired"/> is true,
    /// signature is still verified but token age is bounded by <see cref="ResendGraceWindow"/>
    /// instead of the strict <see cref="ChallengeLifetime"/> — used by the
    /// resend endpoint so users with a 5-min-old code can request a new one
    /// without re-entering their password.
    /// </summary>
    Result<TwoFactorChallengeClaims> ValidateChallenge(string challengeToken, bool allowExpired = false);
}

public sealed record TwoFactorChallengeClaims(
    string UserId,
    string Email,
    string CodeHash,
    DateTime IssuedAt,
    DateTime ExpiresAt);
