using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <summary>
    /// Backfills <c>PointsBoxXPercent</c> / <c>PointsBoxYPercent</c> on
    /// CorporateContests rows that were inserted before the previous
    /// migration's database-level default was corrected from 0 to 50.
    /// EF only applies the C# default on inserts done by the application,
    /// so rows that landed in the table while the column default was 0
    /// remained at 0/0 and the points overlay rendered at the banner's
    /// top-left corner. Idempotent — UPDATEing already-50 rows is a no-op.
    /// </summary>
    public partial class BackfillContestPointsBoxDefaults : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE CorporateContests
                   SET PointsBoxXPercent = 50,
                       PointsBoxYPercent = 50
                 WHERE PointsBoxXPercent = 0
                   AND PointsBoxYPercent = 0;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op — the previous defaults were 0/0 and reverting would
            // re-introduce the broken positioning bug.
        }
    }
}
