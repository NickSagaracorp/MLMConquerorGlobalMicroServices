using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class CompraDeTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPurchasable",
                table: "TokenTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TokenPurchaseId",
                table: "TokenTransactions",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Purpose",
                table: "CryptoPaymentConfirmations",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "TokenPurchases",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    TokenTypeId = table.Column<int>(type: "int", nullable: false),
                    GrantedProductIds = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PeriodKey = table.Column<int>(type: "int", nullable: false),
                    CreditCardId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    PaymentHistoryId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_TokenPurchases", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TokenTransactions_OriginalOwnerMemberId_TransactionType_CreationDate",
                table: "TokenTransactions",
                columns: new[] { "OriginalOwnerMemberId", "TransactionType", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TokenTransactions_TokenPurchaseId",
                table: "TokenTransactions",
                column: "TokenPurchaseId",
                filter: "[TokenPurchaseId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CryptoPaymentConfirmations_Purpose_Status_CreationDate",
                table: "CryptoPaymentConfirmations",
                columns: new[] { "Purpose", "Status", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TokenPurchases_MemberId_IdempotencyKey",
                table: "TokenPurchases",
                columns: new[] { "MemberId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokenPurchases_MemberId_PeriodKey_Status",
                table: "TokenPurchases",
                columns: new[] { "MemberId", "PeriodKey", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TokenPurchases_OrderId",
                table: "TokenPurchases",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokenPurchases_Status",
                table: "TokenPurchases",
                column: "Status");

            // Valor inicial de IsPurchasable (Plan F, decisión D1). Nada en los datos distingue las
            // variantes promocionales salvo el nombre, así que se usa UNA vez para inicializar; desde
            // aquí manda la columna, que el admin cambia en Token Types. Upgrade (Category = 2) no
            // otorga productos y no se vende de todas formas.
            migrationBuilder.Sql(@"
UPDATE TokenTypes
   SET IsPurchasable = 1
 WHERE IsActive = 1
   AND Category <> 2
   AND Name NOT LIKE '%FREE%'
   AND Name NOT LIKE '%Coupon%'
   AND Name NOT LIKE '%SpecialPromo%'
   AND Name NOT LIKE '%Help a Friend%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TokenPurchases");

            migrationBuilder.DropIndex(
                name: "IX_TokenTransactions_OriginalOwnerMemberId_TransactionType_CreationDate",
                table: "TokenTransactions");

            migrationBuilder.DropIndex(
                name: "IX_TokenTransactions_TokenPurchaseId",
                table: "TokenTransactions");

            migrationBuilder.DropIndex(
                name: "IX_CryptoPaymentConfirmations_Purpose_Status_CreationDate",
                table: "CryptoPaymentConfirmations");

            migrationBuilder.DropColumn(
                name: "IsPurchasable",
                table: "TokenTypes");

            migrationBuilder.DropColumn(
                name: "TokenPurchaseId",
                table: "TokenTransactions");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "CryptoPaymentConfirmations");
        }
    }
}
