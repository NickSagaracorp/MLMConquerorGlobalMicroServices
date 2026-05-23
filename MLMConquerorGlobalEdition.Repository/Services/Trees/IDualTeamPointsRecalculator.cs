namespace MLMConquerorGlobalEdition.Repository.Services.Trees;

/// <summary>
/// Walks up the Dual Team (binary) tree from a newly-placed (or moved) member
/// and recomputes <c>LeftLegPoints</c> / <c>RightLegPoints</c> on every ancestor.
///
/// Also mirrors the resulting total onto <c>MemberStatistics.DualTeamPoints</c>
/// so rank evaluation, dashboards and reports see the same number.
///
/// Ghost points are NOT included — only organic tree points.
///
/// This is the SINGLE source of truth for upline-stats recomputation after a
/// placement. Both BizCenter's <c>PlaceMemberHandler</c> and SignupAPI's
/// <c>PlaceMemberHandler</c> must call this — otherwise leg points drift after
/// SignupAPI-initiated placements (Sprint-15 Bug C).
/// </summary>
public interface IDualTeamPointsRecalculator
{
    /// <summary>
    /// Recalculates leg points for every ancestor of <paramref name="startMemberId"/>
    /// (typically the placement target parent) and persists the changes.
    /// </summary>
    Task RecalculateForUplinesAsync(string startMemberId, CancellationToken ct = default);
}
