using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyResidualEarningPaymentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommentedBy",
                table: "DailyResidualEarnings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentComment",
                table: "DailyResidualEarnings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentDate",
                table: "DailyResidualEarnings",
                type: "datetime2",
                nullable: true);

            // Backfill the 38 rows inserted by the prior backfill migration
            // (CreatedBy='backfill-migration', Status=Paid).
            // PaymentDate = EarnedDate (best available historical proxy).
            // CommentedBy = 'backfill-migration' (signals these were set by migration, not a live job).
            // PaymentComment is left NULL — historical accrual origin cannot be reconstructed.
            migrationBuilder.Sql(@"
                UPDATE DailyResidualEarnings
                SET    PaymentDate  = EarnedDate,
                       CommentedBy  = N'backfill-migration'
                WHERE  CreatedBy    = N'backfill-migration'
                  AND  Status       = 2;  -- CommissionEarningStatus.Paid
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommentedBy",
                table: "DailyResidualEarnings");

            migrationBuilder.DropColumn(
                name: "PaymentComment",
                table: "DailyResidualEarnings");

            migrationBuilder.DropColumn(
                name: "PaymentDate",
                table: "DailyResidualEarnings");
        }
    }
}
