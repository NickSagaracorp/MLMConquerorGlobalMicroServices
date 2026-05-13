using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Member;

/// <summary>
/// Monthly snapshot of <see cref="MemberStatisticEntity"/> + dual-team leg
/// points. One row per (MemberId, SnapshotYear, SnapshotMonth) — written by
/// the nightly snapshot job using upsert semantics so the row for the
/// current month gets refreshed every night until the month ends.
///
/// Powers the 6-month historical charts on the BizCenter / Admin Residuals
/// page (Total Dual Team Points trend, ET points trend, income growth, etc.).
/// </summary>
public class MemberStatisticHistoryEntity : AuditChangesLongKey
{
    public required string MemberId { get; set; }

    /// <summary>Calendar year of the snapshot (UTC).</summary>
    public int SnapshotYear  { get; set; }

    /// <summary>Calendar month of the snapshot (1..12, UTC).</summary>
    public int SnapshotMonth { get; set; }

    // Mirror of MemberStatisticEntity ─────────────────────────────────────────
    public int     PersonalPoints                       { get; set; }
    public int     ExternalCustomerPoints               { get; set; }
    public int     DualTeamSize                         { get; set; }
    public int     EnrollmentTeamSize                   { get; set; }
    public int     DualTeamPoints                       { get; set; }
    public int     EnrollmentPoints                     { get; set; }
    public int     QualifiedSponsoredMembers            { get; set; }
    public int     QualifiedSponsoredExternalCustomers  { get; set; }
    public int     EnrollmentTeamGrowth                 { get; set; }
    public int     DualteamGrowth                       { get; set; }
    public int     EnrollmentTeamPointsGrowth           { get; set; }
    public int     DualTeamPointsGrowth                 { get; set; }
    public decimal CurrentWeekIncomeGrowth              { get; set; }
    public decimal CurrentMonthIncomeGrowth             { get; set; }
    public decimal CurrentYearIncomeGrowth              { get; set; }

    // Additional DT context joined from DualTeamTree (not in MemberStatistic
    // entity but needed by the residuals chart).
    public decimal LeftLegPoints  { get; set; }
    public decimal RightLegPoints { get; set; }
}
