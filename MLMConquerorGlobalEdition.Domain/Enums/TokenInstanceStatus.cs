namespace MLMConquerorGlobalEdition.Domain.Enums;

/// <summary>
/// Lifecycle status of a redeemable token instance (TokenTransaction with a non-null ReferenceId / TokenCode).
///
/// - Issued      : just created and assigned to its original owner; never been transferred. Eligible for redeem.
/// - Distributed : transferred at least once from its original owner to a downstream member. Eligible for redeem.
/// - Used        : consumed at signup. Cannot be redeemed again.
/// - Voided      : invalidated by admin (e.g., chargeback recovery). Cannot be redeemed.
/// - Expired     : passed its ExpiresAt date without redemption. Cannot be redeemed.
/// </summary>
public enum TokenInstanceStatus
{
    Issued      = 0,
    Distributed = 1,
    Used        = 2,
    Voided      = 3,
    Expired     = 4
}
