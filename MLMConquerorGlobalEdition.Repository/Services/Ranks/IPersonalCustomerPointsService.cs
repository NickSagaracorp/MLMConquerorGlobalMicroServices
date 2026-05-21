namespace MLMConquerorGlobalEdition.Repository.Services.Ranks;

/// <summary>
/// Single source of truth for Personal Customer Points (PCP) — the personal-points
/// quantity the universal rank gate evaluates. PCP = own active-membership points +
/// the active-membership points of every directly-sponsored member.
/// Distinct from MemberStatisticEntity.PersonalPoints (used by FSB/commissions).
/// </summary>
public interface IPersonalCustomerPointsService
{
    /// <summary>Own active-membership points + Σ active-membership points of directly-sponsored members.</summary>
    Task<int> GetPersonalCustomerPointsAsync(string memberId, CancellationToken ct = default);

    /// <summary>
    /// One person's membership points: the sum of Product.QualificationPoins across every product
    /// on the order of that person's ACTIVE MembershipSubscription. 0 when the membership is not Active.
    /// </summary>
    Task<int> GetMembershipPointsAsync(string memberId, CancellationToken ct = default);
}
