using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenInstanceLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UsedByMemberId",
                table: "TokenTransactions",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceId",
                table: "TokenTransactions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MemberId",
                table: "TokenTransactions",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "DistributedToMemberId",
                table: "TokenTransactions",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "TokenTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalOwnerMemberId",
                table: "TokenTransactions",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousOwnerMemberId",
                table: "TokenTransactions",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "TokenTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UsedOnOrderId",
                table: "TokenTransactions",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true);

            // Backfill instance lifecycle for tokens issued before this migration.
            // Only rows that actually carry a ReferenceId (TokenCode) are real redeemable
            // instances — pure ledger rows (Distributed/Used events) keep Status=Issued (0)
            // but they're filtered out everywhere by ReferenceId IS NOT NULL.
            //
            //   OriginalOwnerMemberId ← current MemberId (best-effort: history is unrecoverable)
            //   Status                ← 2 (Used) when UsedAt is set, otherwise 0 (Issued)
            migrationBuilder.Sql(@"
                UPDATE [TokenTransactions]
                SET [OriginalOwnerMemberId] = [MemberId],
                    [Status]                = CASE WHEN [UsedAt] IS NOT NULL THEN 2 ELSE 0 END
                WHERE [ReferenceId] IS NOT NULL;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_TokenTransactions_MemberId_Status_ReferenceId",
                table: "TokenTransactions",
                columns: new[] { "MemberId", "Status", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_TokenTransactions_ReferenceId",
                table: "TokenTransactions",
                column: "ReferenceId",
                unique: true,
                filter: "[ReferenceId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TokenTransactions_MemberId_Status_ReferenceId",
                table: "TokenTransactions");

            migrationBuilder.DropIndex(
                name: "IX_TokenTransactions_ReferenceId",
                table: "TokenTransactions");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "TokenTransactions");

            migrationBuilder.DropColumn(
                name: "OriginalOwnerMemberId",
                table: "TokenTransactions");

            migrationBuilder.DropColumn(
                name: "PreviousOwnerMemberId",
                table: "TokenTransactions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "TokenTransactions");

            migrationBuilder.DropColumn(
                name: "UsedOnOrderId",
                table: "TokenTransactions");

            migrationBuilder.AlterColumn<string>(
                name: "UsedByMemberId",
                table: "TokenTransactions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(36)",
                oldMaxLength: 36,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceId",
                table: "TokenTransactions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MemberId",
                table: "TokenTransactions",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(36)",
                oldMaxLength: 36);

            migrationBuilder.AlterColumn<string>(
                name: "DistributedToMemberId",
                table: "TokenTransactions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(36)",
                oldMaxLength: 36,
                oldNullable: true);
        }
    }
}
