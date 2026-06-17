using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddPayoutGatewayAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayoutAttemptEarnings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayoutAttemptId = table.Column<long>(type: "bigint", nullable: false),
                    CommissionEarningId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutAttemptEarnings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayoutAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WalletTypeSnapshot = table.Column<int>(type: "int", nullable: false),
                    PayoutAccountSnapshot = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PayoutAccountMetaSnapshot = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AmountUsd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ProcessDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GatewayTransactionId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GatewayErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GatewayErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AttemptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LatencyMs = table.Column<long>(type: "bigint", nullable: true),
                    EarningsCount = table.Column<int>(type: "int", nullable: false),
                    DisbursementMode = table.Column<int>(type: "int", nullable: false),
                    PayoutBatchId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReceiptUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReceiptSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ReceiptLedgerSeq = table.Column<long>(type: "bigint", nullable: true),
                    ReceiptPrevHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ReceiptAnchorRef = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayoutBatches",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WalletType = table.Column<int>(type: "int", nullable: false),
                    ProcessDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ExportCsvUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResultCsvUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MemberCount = table.Column<int>(type: "int", nullable: false),
                    TotalAmountUsd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReconciledBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReconciledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayoutGatewaySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletType = table.Column<int>(type: "int", nullable: false),
                    MinimumPayoutAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutGatewaySettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutAttemptEarnings_CommissionEarningId",
                table: "PayoutAttemptEarnings",
                column: "CommissionEarningId");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutAttemptEarnings_PayoutAttemptId",
                table: "PayoutAttemptEarnings",
                column: "PayoutAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutAttempts_MemberId_ProcessDateUtc",
                table: "PayoutAttempts",
                columns: new[] { "MemberId", "ProcessDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutAttempts_Outcome_ProcessDateUtc",
                table: "PayoutAttempts",
                columns: new[] { "Outcome", "ProcessDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutAttempts_PayoutBatchId",
                table: "PayoutAttempts",
                column: "PayoutBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutBatches_WalletType_ProcessDateUtc",
                table: "PayoutBatches",
                columns: new[] { "WalletType", "ProcessDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutGatewaySettings_WalletType",
                table: "PayoutGatewaySettings",
                column: "WalletType",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayoutAttemptEarnings");

            migrationBuilder.DropTable(
                name: "PayoutAttempts");

            migrationBuilder.DropTable(
                name: "PayoutBatches");

            migrationBuilder.DropTable(
                name: "PayoutGatewaySettings");
        }
    }
}
