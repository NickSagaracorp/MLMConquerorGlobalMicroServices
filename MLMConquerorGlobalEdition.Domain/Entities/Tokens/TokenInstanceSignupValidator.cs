using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;

namespace MLMConquerorGlobalEdition.Domain.Entities.Tokens;

/// <summary>
/// Pure-domain validator that decides whether a redeemable token instance can be used
/// to complete a NEW SIGNUP under a given sponsor with a given product selection.
///
/// Order of checks (fail-fast):
///   1. Status must be Issued or Distributed.        → TokenAlreadyUsedException (generic msg)
///   2. ExpiresAt, if set, must be in the future.    → TokenExpiredException (generic msg)
///   3. TokenType must be Active and Category=Enrollment.
///                                                    → TokenAlreadyUsedException (generic msg — Upgrade tokens are not legal in signup)
///   4. Current owner (TokenTransaction.MemberId) must equal sponsorMemberId.
///                                                    → TokenNotOwnedBySponsorException (generic msg)
///   5. Selected products must be a subset of the token's Role=Granted products.
///                                                    → TokenProductMismatchException (specific msg)
///
/// Generic-message exceptions intentionally collapse to the same error code "TOKEN_NOT_VALID"
/// so a caller cannot distinguish "not yours" from "already used" from "expired" — only the
/// product-mismatch case carries a different code with details.
/// </summary>
public static class TokenInstanceSignupValidator
{
    /// <summary>
    /// Throws a domain exception describing the first rule that fails. Returns silently on success.
    /// </summary>
    /// <param name="instance">The TokenTransaction row representing the redeemable instance (ReferenceId != null).</param>
    /// <param name="tokenType">The TokenType for this instance (Category, IsActive consulted).</param>
    /// <param name="grantedProducts">All TokenTypeProduct rows for this TokenType — only those with Role=Granted are used.</param>
    /// <param name="sponsorMemberId">MemberId of the sponsor under whom the signup is being performed.</param>
    /// <param name="selectedProductIds">Product IDs the user picked in the signup wizard.</param>
    /// <param name="now">Current UTC time (use IDateTimeProvider in the caller).</param>
    public static void Validate(
        TokenTransaction instance,
        TokenType tokenType,
        IReadOnlyCollection<TokenTypeProduct> grantedProducts,
        string sponsorMemberId,
        IReadOnlyCollection<string> selectedProductIds,
        DateTime now)
    {
        if (instance is null)              throw new ArgumentNullException(nameof(instance));
        if (tokenType is null)             throw new ArgumentNullException(nameof(tokenType));
        if (grantedProducts is null)       throw new ArgumentNullException(nameof(grantedProducts));
        if (selectedProductIds is null)    throw new ArgumentNullException(nameof(selectedProductIds));

        if (string.IsNullOrEmpty(instance.ReferenceId))
            throw new TokenAlreadyUsedException();

        // 1. Status must be redeemable
        if (instance.Status is not (TokenInstanceStatus.Issued or TokenInstanceStatus.Distributed))
            throw new TokenAlreadyUsedException();

        // 2. Not expired
        if (instance.ExpiresAt is not null && instance.ExpiresAt.Value <= now)
            throw new TokenExpiredException();

        // 3. TokenType must be active enrollment-style
        //    (Upgrade tokens are not legal in a NEW signup flow.)
        if (!tokenType.IsActive ||
            tokenType.Category is not (TokenCategory.Enrollment or TokenCategory.Monthly or TokenCategory.Annual))
        {
            throw new TokenAlreadyUsedException();
        }

        // 4. Current owner must equal sponsor
        if (!string.Equals(instance.MemberId, sponsorMemberId, StringComparison.OrdinalIgnoreCase))
            throw new TokenNotOwnedBySponsorException();

        // 5. Product subset check
        var allowed = grantedProducts
            .Where(p => p.Role == TokenProductRole.Granted)
            .Select(p => p.ProductId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var selected = selectedProductIds
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (selected.Count == 0 || !selected.IsSubsetOf(allowed))
        {
            // Build a list of allowed product IDs for the message. The caller can later
            // resolve names from the database — at the domain layer we only have IDs.
            var allowedList = string.Join(", ", allowed.OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
            throw new TokenProductMismatchException(allowedList);
        }
    }
}
