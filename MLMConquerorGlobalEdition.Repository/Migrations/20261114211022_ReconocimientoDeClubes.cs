using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class ReconocimientoDeClubes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecognitionLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Threshold = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    WindowDays = table.Column<int>(type: "int", nullable: true),
                    Reward = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RewardNeedsSize = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecognitionLevels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecognitionOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequiresReason = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecognitionOptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemberRecognitions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecognitionLevelId = table.Column<int>(type: "int", nullable: false),
                    ReachedDate = table.Column<DateTime>(type: "date", nullable: false),
                    QualifyingTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    CeoCallMonth = table.Column<DateTime>(type: "date", nullable: true),
                    CeoCallOptionId = table.Column<int>(type: "int", nullable: true),
                    CeoCallReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EventId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    EventOptionId = table.Column<int>(type: "int", nullable: true),
                    EventReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RewardSize = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberRecognitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberRecognitions_CorporateEvents_EventId",
                        column: x => x.EventId,
                        principalTable: "CorporateEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberRecognitions_RecognitionLevels_RecognitionLevelId",
                        column: x => x.RecognitionLevelId,
                        principalTable: "RecognitionLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberRecognitions_RecognitionOptions_CeoCallOptionId",
                        column: x => x.CeoCallOptionId,
                        principalTable: "RecognitionOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberRecognitions_RecognitionOptions_EventOptionId",
                        column: x => x.EventOptionId,
                        principalTable: "RecognitionOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "RecognitionLevels",
                columns: new[] { "Id", "CreatedBy", "CreationDate", "IsActive", "LastUpdateBy", "LastUpdateDate", "Name", "Reward", "RewardNeedsSize", "SortOrder", "Threshold", "WindowDays" },
                values: new object[,]
                {
                    { 1, "system", new DateTime(2026, 11, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, null, "100k Earner", null, false, 1, 50000m, 180 },
                    { 2, "system", new DateTime(2026, 11, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, null, "500k Earner", null, false, 2, 250000m, 180 },
                    { 3, "system", new DateTime(2026, 11, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, null, "Million Dollar Earner", null, false, 3, 500000m, 180 },
                    { 4, "system", new DateTime(2026, 11, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, null, "100k Lifetime Earner", null, false, 4, 100000m, null },
                    { 5, "system", new DateTime(2026, 11, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, null, "500k Lifetime Earner", null, false, 5, 500000m, null },
                    { 6, "system", new DateTime(2026, 11, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, null, "Lifetime Million Dollar Earner", null, false, 6, 1000000m, null }
                });

            migrationBuilder.InsertData(
                table: "RecognitionOptions",
                columns: new[] { "Id", "CreatedBy", "CreationDate", "IsActive", "LastUpdateBy", "LastUpdateDate", "Name", "RequiresReason", "SortOrder" },
                values: new object[,]
                {
                    { 1, "system", new DateTime(2026, 11, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, null, "Recognized Higher Level", false, 1 },
                    { 2, "system", new DateTime(2026, 11, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, null, "Recognized Long Time Ago", false, 2 },
                    { 3, "system", new DateTime(2026, 11, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, null, "Do not recognize", true, 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberRecognitions_CeoCallOptionId",
                table: "MemberRecognitions",
                column: "CeoCallOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberRecognitions_EventId",
                table: "MemberRecognitions",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberRecognitions_EventOptionId",
                table: "MemberRecognitions",
                column: "EventOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberRecognitions_RecognitionLevelId_ReachedDate",
                table: "MemberRecognitions",
                columns: new[] { "RecognitionLevelId", "ReachedDate" });

            migrationBuilder.CreateIndex(
                name: "UX_MemberRecognitions_Member_Level",
                table: "MemberRecognitions",
                columns: new[] { "MemberId", "RecognitionLevelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecognitionLevels_Name",
                table: "RecognitionLevels",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecognitionOptions_Name",
                table: "RecognitionOptions",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberRecognitions");

            migrationBuilder.DropTable(
                name: "RecognitionLevels");

            migrationBuilder.DropTable(
                name: "RecognitionOptions");
        }
    }
}
