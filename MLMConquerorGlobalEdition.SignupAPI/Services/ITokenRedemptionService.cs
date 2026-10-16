using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.Services;

/// <summary>
/// Validates and consumes a token instance during signup completion.
/// On success, marks the instance Used, writes a ledger TokenTransaction, and returns Success.
/// On failure, returns a Result with a generic error code (TOKEN_NOT_VALID) or the specific
/// product-mismatch code (TOKEN_PRODUCT_MISMATCH) — never throws to the caller.
///
/// <paramref name="expectedOwnerMemberId"/>: the owner the caller read BEFORE taking the funds lock
/// of that owner (CompleteSignupHandler). The instance is re-read tracked here, under the lock; if
/// its owner changed meanwhile (a distribution or transfer moved it), the lock held is not the
/// owner's and the redemption is refused (TOKEN_NOT_VALID). Null skips the check (callers that do
/// not hold the lock; the concurrency tokens on MemberId/Status still stop a lost update).
/// </summary>
public interface ITokenRedemptionService
{
    Task<Result<bool>> RedeemForSignupAsync(
        string tokenCode,
        string newMemberId,
        string orderId,
        IReadOnlyCollection<string> selectedProductIds,
        string? expectedOwnerMemberId,
        DateTime now,
        CancellationToken ct);
}
