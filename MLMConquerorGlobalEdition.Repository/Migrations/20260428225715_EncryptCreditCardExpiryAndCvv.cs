using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class EncryptCreditCardExpiryAndCvv : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Old rows have plaintext ExpiryMonth/Year that cannot be re-encrypted
            // without the CVV (which we never had on those rows anyway). Drop them
            // — only test cards exist in dev today, and the new flow will recreate
            // them encrypted end-to-end.
            migrationBuilder.Sql("DELETE FROM CreditCards;");

            migrationBuilder.DropColumn(
                name: "ExpiryMonth",
                table: "CreditCards");

            migrationBuilder.DropColumn(
                name: "ExpiryYear",
                table: "CreditCards");

            migrationBuilder.AddColumn<string>(
                name: "EncryptedCvv",
                table: "CreditCards",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedExpiry",
                table: "CreditCards",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EncryptedCvv",
                table: "CreditCards");

            migrationBuilder.DropColumn(
                name: "EncryptedExpiry",
                table: "CreditCards");

            migrationBuilder.AddColumn<int>(
                name: "ExpiryMonth",
                table: "CreditCards",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExpiryYear",
                table: "CreditCards",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
