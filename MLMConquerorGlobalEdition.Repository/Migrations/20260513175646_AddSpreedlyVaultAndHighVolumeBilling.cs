using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddSpreedlyVaultAndHighVolumeBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ShardKey",
                table: "SubscriptionBillingStates",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<int>(
                name: "DualTeamContribution",
                table: "MembershipSubscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EnrollmentContribution",
                table: "MembershipSubscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PersonalContribution",
                table: "MembershipSubscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SpreedlyPaymentMethodToken",
                table: "CreditCards",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpreedlyGatewayTokenEncrypted",
                table: "ApiCredentials",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CommissionTriggerQueues",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OrderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TriggerType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsProcessed = table.Column<bool>(type: "bit", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionTriggerQueues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PointDeltaEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    OrderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    DualTeamDelta = table.Column<int>(type: "int", nullable: false),
                    EnrollmentDelta = table.Column<int>(type: "int", nullable: false),
                    PersonalDelta = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointDeltaEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecurringBillingBatches",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RunDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Gateway = table.Column<int>(type: "int", nullable: false),
                    WorkerCount = table.Column<int>(type: "int", nullable: false),
                    ScheduledStartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CaseCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_RecurringBillingBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecurringBillingBatchShards",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BatchId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ShardIndex = table.Column<int>(type: "int", nullable: false),
                    IdRangeStart = table.Column<long>(type: "bigint", nullable: false),
                    IdRangeEnd = table.Column<long>(type: "bigint", nullable: false),
                    AssignedWorkerKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CasesProcessed = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_RecurringBillingBatchShards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringBillingBatchShards_RecurringBillingBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "RecurringBillingBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionBillingStates_ShardKey",
                table: "SubscriptionBillingStates",
                column: "ShardKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommissionTriggerQueues_BatchId_IsProcessed",
                table: "CommissionTriggerQueues",
                columns: new[] { "BatchId", "IsProcessed" });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionTriggerQueues_MemberId",
                table: "CommissionTriggerQueues",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_PointDeltaEvents_BatchId_Status",
                table: "PointDeltaEvents",
                columns: new[] { "BatchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PointDeltaEvents_MemberId",
                table: "PointDeltaEvents",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBillingBatches_RunDate_Gateway",
                table: "RecurringBillingBatches",
                columns: new[] { "RunDate", "Gateway" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBillingBatches_Status",
                table: "RecurringBillingBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBillingBatchShards_BatchId_ShardIndex",
                table: "RecurringBillingBatchShards",
                columns: new[] { "BatchId", "ShardIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBillingBatchShards_BatchId_Status",
                table: "RecurringBillingBatchShards",
                columns: new[] { "BatchId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommissionTriggerQueues");

            migrationBuilder.DropTable(
                name: "PointDeltaEvents");

            migrationBuilder.DropTable(
                name: "RecurringBillingBatchShards");

            migrationBuilder.DropTable(
                name: "RecurringBillingBatches");

            migrationBuilder.DropIndex(
                name: "IX_SubscriptionBillingStates_ShardKey",
                table: "SubscriptionBillingStates");

            migrationBuilder.DropColumn(
                name: "ShardKey",
                table: "SubscriptionBillingStates");

            migrationBuilder.DropColumn(
                name: "DualTeamContribution",
                table: "MembershipSubscriptions");

            migrationBuilder.DropColumn(
                name: "EnrollmentContribution",
                table: "MembershipSubscriptions");

            migrationBuilder.DropColumn(
                name: "PersonalContribution",
                table: "MembershipSubscriptions");

            migrationBuilder.DropColumn(
                name: "SpreedlyPaymentMethodToken",
                table: "CreditCards");

            migrationBuilder.DropColumn(
                name: "SpreedlyGatewayTokenEncrypted",
                table: "ApiCredentials");
        }
    }
}
