using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddRankEvaluationQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RankEvaluationQueue",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TriggerMemberId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EvaluateMemberId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TriggerEvent = table.Column<int>(type: "int", nullable: false),
                    TriggerDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsProcessed = table.Column<bool>(type: "bit", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankEvaluationQueue", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RankEvaluationQueue_EvaluateMemberId",
                table: "RankEvaluationQueue",
                column: "EvaluateMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_RankEvaluationQueue_IsProcessed_TriggerDate",
                table: "RankEvaluationQueue",
                columns: new[] { "IsProcessed", "TriggerDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RankEvaluationQueue_TriggerMemberId",
                table: "RankEvaluationQueue",
                column: "TriggerMemberId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RankEvaluationQueue");
        }
    }
}
