using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletApiLogAndExtendHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Action",
                table: "WalletHistories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NewAccountIdentifier",
                table: "WalletHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NewIsPreferred",
                table: "WalletHistories",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OldAccountIdentifier",
                table: "WalletHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OldIsPreferred",
                table: "WalletHistories",
                type: "bit",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WalletApiLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WalletType = table.Column<int>(type: "int", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HttpMethod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    RequestBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HttpStatusCode = table.Column<int>(type: "int", nullable: false),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletApiLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WalletApiLogs_CreationDate",
                table: "WalletApiLogs",
                column: "CreationDate");

            migrationBuilder.CreateIndex(
                name: "IX_WalletApiLogs_MemberId_WalletType",
                table: "WalletApiLogs",
                columns: new[] { "MemberId", "WalletType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WalletApiLogs");

            migrationBuilder.DropColumn(
                name: "Action",
                table: "WalletHistories");

            migrationBuilder.DropColumn(
                name: "NewAccountIdentifier",
                table: "WalletHistories");

            migrationBuilder.DropColumn(
                name: "NewIsPreferred",
                table: "WalletHistories");

            migrationBuilder.DropColumn(
                name: "OldAccountIdentifier",
                table: "WalletHistories");

            migrationBuilder.DropColumn(
                name: "OldIsPreferred",
                table: "WalletHistories");
        }
    }
}
