using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.Services;

/// <summary>
/// Validates and consumes a token instance during signup completion.
/// On success, marks the instance Used, writes a ledger TokenTransaction, and returns Success.
/// On failure, returns a Result with a generic error code (TOKEN_NOT_VALID) or the specific
/// product-mismatch code (TOKEN_PRODUCT_MISMATCH) — never throws to the caller.
/// </summary>
public interface ITokenRedemptionService
{
    Task<Result<bool>> RedeemForSignupAsync(
        string tokenCode,
        string newMemberId,
        string orderId,
        IReadOnlyCollection<string> selectedProductIds,
        DateTime now,
        CancellationToken ct);
}
