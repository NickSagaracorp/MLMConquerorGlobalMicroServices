using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddCryptoPaymentConfirmations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CryptoPaymentConfirmations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OrderId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MemberEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CryptoCurrency = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AmountDue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CryptoTransactionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ConfirmedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ConfirmedByEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CryptoPaymentConfirmations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CryptoPaymentConfirmations_MemberId",
                table: "CryptoPaymentConfirmations",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_CryptoPaymentConfirmations_OrderId",
                table: "CryptoPaymentConfirmations",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CryptoPaymentConfirmations_Status_CreationDate",
                table: "CryptoPaymentConfirmations",
                columns: new[] { "Status", "CreationDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CryptoPaymentConfirmations");
        }
    }
}
