namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;

/// <summary>
/// Top-of-page stat row for the AdminWeb Members grid. Four headline counters
/// that give an admin an instant pulse on the member base without scrolling
/// the grid: who's live, who joined today, who's leaving, who's getting placed.
/// Cached server-side (~30s) so a "Refresh" button click doesn't hammer the DB.
/// </summary>
public class MemberStatsDto
{
    /// <summary>
    /// Live count of <see cref="MLMConquerorGlobalEdition.Domain.Entities.Member.MemberProfile"/>
    /// rows where <c>Status = MemberAccountStatus.Active</c> and <c>IsDeleted = false</c>.
    /// </summary>
    public int TotalActive { get; set; }

    /// <summary>
    /// Count of <see cref="MLMConquerorGlobalEdition.Domain.Entities.Member.MemberProfile"/>
    /// rows whose <c>EnrollDate</c> is within the last 24 hours (computed via
    /// <c>IDateTimeProvider.Now - 24h</c>, never <c>DateTime.UtcNow</c> directly).
    /// </summary>
    public int NewSignupsLast24Hours { get; set; }

    /// <summary>
    /// Count of members who effectively churned in the last 24 hours.
    /// Source signal: <see cref="MLMConquerorGlobalEdition.Domain.Entities.Member.MemberStatusHistory"/>
    /// rows where <c>ChangedAt &gt;= now - 24h</c> and <c>NewStatus</c> is one of
    /// <c>Inactive</c>, <c>Suspended</c>, or <c>Terminated</c>. We chose
    /// MemberStatusHistory (option #1 in the spec) because (a) it's the only
    /// table that captures the moment of cancellation with no ambiguity,
    /// (b) it survives undo/redo on Status (a member flipped back to Active
    /// still has a recorded cancellation event), and (c) it doesn't double-count
    /// when MembershipSubscription is cancelled but the member account stays
    /// Active. MemberAccountStatus has no "Cancelled" value, so Inactive +
    /// Suspended + Terminated is the cancellation signal for accounts.
    /// </summary>
    public int CancellationsLast24Hours { get; set; }

    /// <summary>
    /// Count of <see cref="MLMConquerorGlobalEdition.Domain.Entities.Tree.DualTeamEntity"/>
    /// rows created in the last 24 hours — i.e. binary-tree placements newly
    /// recorded. Includes both auto-placements and admin-driven placements.
    /// </summary>
    public int PlacementsLast24Hours { get; set; }
}
