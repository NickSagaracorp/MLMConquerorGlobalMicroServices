namespace MLMConquerorGlobalEdition.Domain.Constants;

/// <summary>Identifiers for the per-rank "seniority bonus" — a once-per-rank bonus granted when an
/// ambassador holds a rank for ≥14 consecutive days. Each rank has one CommissionType (resolved by
/// CommissionCategoryId == CategoryId AND LifeTimeRank == rankId); a CommissionEarning with that type
/// id is the "already granted" record.</summary>
public static class RankSeniorityBonus
{
    /// <summary>CommissionCategory dedicated to rank seniority bonuses.
    /// Category Id 9 — the next free id after the 8 categories seeded at launch.</summary>
    public const int CategoryId = 9;

    /// <summary>The first CommissionType.Id used for a per-rank seniority type.
    /// Types are contiguous: FirstTypeId + (rankId - 1), where rankId is RankDefinition.Id 1..19.
    /// (CommissionType ids 85 is Car Bonus; seniority block starts at 86.)</summary>
    public const int FirstTypeId = 86;
}
