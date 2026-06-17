using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddSignupRiskFingerprintClearedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SignupRiskFingerprints_VisitorId_CreationDate",
                table: "SignupRiskFingerprints");

            migrationBuilder.AddColumn<string>(
                name: "ClearReason",
                table: "SignupRiskFingerprints",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Cleared",
                table: "SignupRiskFingerprints",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClearedAt",
                table: "SignupRiskFingerprints",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClearedBy",
                table: "SignupRiskFingerprints",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignupRiskFingerprints_VisitorId_CreationDate_Cleared",
                table: "SignupRiskFingerprints",
                columns: new[] { "VisitorId", "CreationDate", "Cleared" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SignupRiskFingerprints_VisitorId_CreationDate_Cleared",
                table: "SignupRiskFingerprints");

            migrationBuilder.DropColumn(
                name: "ClearReason",
                table: "SignupRiskFingerprints");

            migrationBuilder.DropColumn(
                name: "Cleared",
                table: "SignupRiskFingerprints");

            migrationBuilder.DropColumn(
                name: "ClearedAt",
                table: "SignupRiskFingerprints");

            migrationBuilder.DropColumn(
                name: "ClearedBy",
                table: "SignupRiskFingerprints");

            migrationBuilder.CreateIndex(
                name: "IX_SignupRiskFingerprints_VisitorId_CreationDate",
                table: "SignupRiskFingerprints",
                columns: new[] { "VisitorId", "CreationDate" });
        }
    }
}
