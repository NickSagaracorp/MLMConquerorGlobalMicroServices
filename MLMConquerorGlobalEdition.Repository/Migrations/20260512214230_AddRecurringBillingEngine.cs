using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringBillingEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyResidualEarnings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BeneficiaryMemberId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    EarnedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SourceOrderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ConsolidatedIntoCommissionEarningId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyResidualEarnings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GlobalParameters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalParameters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecurringBillingAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubscriptionBillingStateId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AttemptIndex = table.Column<int>(type: "int", nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    FundingSource = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    PaymentHistoryId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OrderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TokenTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    CommissionDeductionEarningId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GatewayChargeAttemptId = table.Column<long>(type: "bigint", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringBillingAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecurringBillingPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CycleType = table.Column<int>(type: "int", nullable: false),
                    RetryCadenceDays = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OnAllRetriesFail = table.Column<int>(type: "int", nullable: false),
                    StopAfterUnbilledDays = table.Column<int>(type: "int", nullable: true),
                    PayFromCommissionBalanceFirst = table.Column<bool>(type: "bit", nullable: false),
                    TokenTypeId = table.Column<int>(type: "int", nullable: true),
                    FixedAmountOverride = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringBillingPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecurringBillingPlanProducts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecurringBillingPlanId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TokenTypeIdOverride = table.Column<int>(type: "int", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringBillingPlanProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringBillingPlanProducts_RecurringBillingPlans_RecurringBillingPlanId",
                        column: x => x.RecurringBillingPlanId,
                        principalTable: "RecurringBillingPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionBillingStates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MembershipSubscriptionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecurringBillingPlanId = table.Column<int>(type: "int", nullable: false),
                    BillingAnchorDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSuccessfulBillingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextBillingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentAttemptIndex = table.Column<int>(type: "int", nullable: false),
                    NextAttemptDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAttemptOutcome = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastFailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionBillingStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionBillingStates_RecurringBillingPlans_RecurringBillingPlanId",
                        column: x => x.RecurringBillingPlanId,
                        principalTable: "RecurringBillingPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyResidualEarnings_BeneficiaryMemberId_Status",
                table: "DailyResidualEarnings",
                columns: new[] { "BeneficiaryMemberId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyResidualEarnings_EarnedDate",
                table: "DailyResidualEarnings",
                column: "EarnedDate");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalParameters_Key",
                table: "GlobalParameters",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBillingAttempts_MemberId_AttemptedAt",
                table: "RecurringBillingAttempts",
                columns: new[] { "MemberId", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBillingAttempts_Outcome_AttemptedAt",
                table: "RecurringBillingAttempts",
                columns: new[] { "Outcome", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBillingAttempts_SubscriptionBillingStateId",
                table: "RecurringBillingAttempts",
                column: "SubscriptionBillingStateId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBillingPlanProducts_ProductId",
                table: "RecurringBillingPlanProducts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBillingPlanProducts_RecurringBillingPlanId_ProductId",
                table: "RecurringBillingPlanProducts",
                columns: new[] { "RecurringBillingPlanId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBillingPlans_CycleType",
                table: "RecurringBillingPlans",
                column: "CycleType");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBillingPlans_IsActive",
                table: "RecurringBillingPlans",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionBillingStates_MemberId",
                table: "SubscriptionBillingStates",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionBillingStates_MembershipSubscriptionId",
                table: "SubscriptionBillingStates",
                column: "MembershipSubscriptionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionBillingStates_RecurringBillingPlanId",
                table: "SubscriptionBillingStates",
                column: "RecurringBillingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionBillingStates_Status_NextAttemptDate",
                table: "SubscriptionBillingStates",
                columns: new[] { "Status", "NextAttemptDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyResidualEarnings");

            migrationBuilder.DropTable(
                name: "GlobalParameters");

            migrationBuilder.DropTable(
                name: "RecurringBillingAttempts");

            migrationBuilder.DropTable(
                name: "RecurringBillingPlanProducts");

            migrationBuilder.DropTable(
                name: "SubscriptionBillingStates");

            migrationBuilder.DropTable(
                name: "RecurringBillingPlans");
        }
    }
}
