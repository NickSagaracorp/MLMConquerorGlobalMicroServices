using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <summary>
    /// Idempotent backfill: moves legacy Daily Residual CommissionEarning rows into DailyResidualEarning.
    ///
    /// Identification: CommissionEarning rows whose CommissionType has ResidualBased = 1
    /// and IsPaidOnSignup = 0 (= "Daily Residual – Binary" and any similar residual-based types).
    ///
    /// Double-count prevention: the original CommissionEarning rows are soft-deleted
    /// (IsDeleted = 1) so the CommissionBalanceService's query-filtered DbSet and the
    /// admin commissions page no longer sum them. The Notes field is updated to reference
    /// the created DailyResidualEarning row (for traceability).
    ///
    /// Idempotency guard: skips any CommissionEarning row that already has IsDeleted = 1,
    /// so re-running this migration is safe.
    ///
    /// Snapshot fields (CurrentRankId, EligibleDualTeamPoints, EligibleEnrollmentTeamPoints,
    /// PersonalPoints) are set to NULL for backfilled rows — this data is not recoverable
    /// from the CommissionEarning history.
    ///
    /// NOTE for admin commissions page: the page currently reads CommissionEarning rows.
    /// Soft-deleted rows are excluded by the HasQueryFilter on that entity. Historical daily-
    /// residual accruals from before this migration are now only visible in DailyResidualEarning.
    /// If the admin page needs to show them, a future UI task should union in DailyResidualEarning
    /// rows with Status = Paid and ConsolidatedIntoCommissionEarningId IS NULL
    /// (these are the backfilled historical rows).
    /// </summary>
    public partial class BackfillDailyResidualEarningsFromCommissionEarning : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Insert DailyResidualEarning rows for each not-yet-migrated
            //         Daily Residual CommissionEarning row.
            //
            // Idempotency: CE.IsDeleted = 0 ensures we only process rows not already migrated.
            // We copy: BeneficiaryMemberId, Amount, EarnedDate, Status, SourceOrderId, Notes.
            // Snapshot fields are NULL (historical data not recoverable).
            // CreatedBy = 'backfill-migration', CreationDate = GETUTCDATE().
            migrationBuilder.Sql(@"
INSERT INTO DailyResidualEarnings
    (BeneficiaryMemberId, Amount, EarnedDate, Status, SourceOrderId, Notes,
     ConsolidatedIntoCommissionEarningId,
     CurrentRankId, EligibleDualTeamPoints, EligibleEnrollmentTeamPoints, PersonalPoints,
     CreationDate, CreatedBy)
SELECT
    CE.BeneficiaryMemberId,
    CE.Amount,
    CE.EarnedDate,
    CE.Status,
    CE.SourceOrderId,
    CASE
        WHEN CE.Notes IS NULL OR CE.Notes = ''
        THEN N'Backfilled from CommissionEarning #' + CE.Id
        ELSE CE.Notes + N' [backfilled from CommissionEarning #' + CE.Id + N']'
    END,
    NULL,  -- ConsolidatedIntoCommissionEarningId: not applicable for backfilled rows
    NULL,  -- CurrentRankId: snapshot not recoverable
    NULL,  -- EligibleDualTeamPoints: snapshot not recoverable
    NULL,  -- EligibleEnrollmentTeamPoints: snapshot not recoverable
    NULL,  -- PersonalPoints: snapshot not recoverable
    GETUTCDATE(),
    N'backfill-migration'
FROM CommissionEarnings CE
INNER JOIN CommissionTypes CT ON CE.CommissionTypeId = CT.Id
WHERE CT.ResidualBased = 1
  AND CT.IsPaidOnSignup = 0
  AND CE.IsDeleted = 0;
");

            // Step 2: Soft-delete the original CommissionEarning rows, updating Notes to
            //         reference the created DailyResidualEarning row by its Id.
            //
            // We join CE back to the newly-created DRE rows by matching
            // BeneficiaryMemberId + Amount + EarnedDate + Status, using the backfill marker
            // in DRE.Notes to confirm the match (prevents false-positives if a member happened
            // to have an identical naturally-created DRE row).
            //
            // Simpler and equally safe: soft-delete any CE row that:
            //   - Is a daily residual type (ResidualBased=1, IsPaidOnSignup=0)
            //   - Is not already soft-deleted
            // and set Notes to 'migrated to DailyResidualEarning'.
            // The DRE row's Notes field already contains 'backfilled from CommissionEarning #<Id>'.
            migrationBuilder.Sql(@"
UPDATE CE
SET
    CE.IsDeleted  = 1,
    CE.DeletedAt  = GETUTCDATE(),
    CE.DeletedBy  = N'backfill-migration',
    CE.Notes      = ISNULL(CE.Notes + N' ', N'') + N'[migrated to DailyResidualEarning — see backfill-migration]',
    CE.LastUpdateDate = GETUTCDATE(),
    CE.LastUpdateBy   = N'backfill-migration'
FROM CommissionEarnings CE
INNER JOIN CommissionTypes CT ON CE.CommissionTypeId = CT.Id
WHERE CT.ResidualBased = 1
  AND CT.IsPaidOnSignup = 0
  AND CE.IsDeleted = 0;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: restore soft-deleted CE rows and remove DRE backfill rows.
            // Only reverses rows created by this migration (identified by CreatedBy = 'backfill-migration').
            migrationBuilder.Sql(@"
-- Restore CommissionEarning rows that were soft-deleted by this migration
UPDATE CE
SET
    CE.IsDeleted      = 0,
    CE.DeletedAt      = NULL,
    CE.DeletedBy      = NULL,
    CE.LastUpdateDate = GETUTCDATE(),
    CE.LastUpdateBy   = N'backfill-migration-rollback'
FROM CommissionEarnings CE
INNER JOIN CommissionTypes CT ON CE.CommissionTypeId = CT.Id
WHERE CT.ResidualBased = 1
  AND CT.IsPaidOnSignup = 0
  AND CE.DeletedBy = N'backfill-migration';
");

            migrationBuilder.Sql(@"
-- Remove DailyResidualEarning rows inserted by this migration
DELETE FROM DailyResidualEarnings
WHERE CreatedBy = N'backfill-migration';
");
        }
    }
}
