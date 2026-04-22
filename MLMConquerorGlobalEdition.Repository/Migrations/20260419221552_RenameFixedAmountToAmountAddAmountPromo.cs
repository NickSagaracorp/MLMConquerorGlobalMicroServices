using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class RenameFixedAmountToAmountAddAmountPromo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FixedAmount",
                table: "CommissionTypes",
                newName: "AmountPromo");

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "CommissionTypes",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 20m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 40m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 40m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 150m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 150m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 150m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 4m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 10m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 15m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 25m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 40m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 80m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 150m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 300m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 500m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 750m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 1000m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 1500m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 2000m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 3000m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 4000m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 22,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 5000m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 23,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 7500m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 24,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 10000m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 25,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 15000m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 26,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 600m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 27,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 1200m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 28,
                column: "Amount",
                value: null);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 29,
                column: "Amount",
                value: null);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 30,
                column: "Amount",
                value: null);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 31,
                column: "Amount",
                value: null);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 32,
                column: "Amount",
                value: null);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 33,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 25m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 34,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 60m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 35,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 120m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 36,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 30m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 37,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 80m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 38,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 60m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 39,
                column: "Amount",
                value: null);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 40,
                columns: new[] { "Amount", "AmountPromo", "Description" },
                values: new object[] { 1m, null, "Deduction applied in real-time when a member consumes tokens. Unit cost can be overridden per TokenType; Amount here is the platform default." });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 41,
                column: "Amount",
                value: null);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 42,
                column: "Amount",
                value: null);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 43,
                column: "Amount",
                value: null);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 44,
                column: "Amount",
                value: null);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 45,
                column: "Amount",
                value: null);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 46,
                column: "Amount",
                value: null);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 47,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 10m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 48,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 20m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 49,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 30m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 50,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 40m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 51,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 50m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 52,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 60m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 53,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 65m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 54,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 70m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 55,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 75m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 56,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 80m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 57,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 85m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 58,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 90m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 59,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 95m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 60,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 100m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 61,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 105m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 62,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 110m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 63,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 115m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 64,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 120m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 65,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 125m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 66,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 10m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 67,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 20m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 68,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 30m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 69,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 40m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 70,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 50m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 71,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 60m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 72,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 65m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 73,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 70m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 74,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 75m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 75,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 80m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 76,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 85m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 77,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 90m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 78,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 95m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 79,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 100m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 80,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 105m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 81,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 110m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 82,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 115m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 83,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 120m, null });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 84,
                columns: new[] { "Amount", "AmountPromo" },
                values: new object[] { 125m, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Amount",
                table: "CommissionTypes");

            migrationBuilder.RenameColumn(
                name: "AmountPromo",
                table: "CommissionTypes",
                newName: "FixedAmount");

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 1,
                column: "FixedAmount",
                value: 20m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 2,
                column: "FixedAmount",
                value: 40m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 3,
                column: "FixedAmount",
                value: 40m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 4,
                column: "FixedAmount",
                value: 150m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 5,
                column: "FixedAmount",
                value: 150m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 6,
                column: "FixedAmount",
                value: 150m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 7,
                column: "FixedAmount",
                value: 4m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 8,
                column: "FixedAmount",
                value: 10m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 9,
                column: "FixedAmount",
                value: 15m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 10,
                column: "FixedAmount",
                value: 25m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 11,
                column: "FixedAmount",
                value: 40m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 12,
                column: "FixedAmount",
                value: 80m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 13,
                column: "FixedAmount",
                value: 150m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 14,
                column: "FixedAmount",
                value: 300m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 15,
                column: "FixedAmount",
                value: 500m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 16,
                column: "FixedAmount",
                value: 750m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 17,
                column: "FixedAmount",
                value: 1000m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 18,
                column: "FixedAmount",
                value: 1500m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 19,
                column: "FixedAmount",
                value: 2000m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 20,
                column: "FixedAmount",
                value: 3000m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 21,
                column: "FixedAmount",
                value: 4000m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 22,
                column: "FixedAmount",
                value: 5000m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 23,
                column: "FixedAmount",
                value: 7500m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 24,
                column: "FixedAmount",
                value: 10000m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 25,
                column: "FixedAmount",
                value: 15000m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 26,
                column: "FixedAmount",
                value: 600m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 27,
                column: "FixedAmount",
                value: 1200m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 33,
                column: "FixedAmount",
                value: 25m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 34,
                column: "FixedAmount",
                value: 60m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 35,
                column: "FixedAmount",
                value: 120m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 36,
                column: "FixedAmount",
                value: 30m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 37,
                column: "FixedAmount",
                value: 80m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 38,
                column: "FixedAmount",
                value: 60m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 40,
                columns: new[] { "Description", "FixedAmount" },
                values: new object[] { "Deduction applied in real-time when a member consumes tokens. Unit cost can be overridden per TokenType; FixedAmount here is the platform default.", 1m });

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 47,
                column: "FixedAmount",
                value: 10m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 48,
                column: "FixedAmount",
                value: 20m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 49,
                column: "FixedAmount",
                value: 30m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 50,
                column: "FixedAmount",
                value: 40m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 51,
                column: "FixedAmount",
                value: 50m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 52,
                column: "FixedAmount",
                value: 60m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 53,
                column: "FixedAmount",
                value: 65m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 54,
                column: "FixedAmount",
                value: 70m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 55,
                column: "FixedAmount",
                value: 75m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 56,
                column: "FixedAmount",
                value: 80m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 57,
                column: "FixedAmount",
                value: 85m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 58,
                column: "FixedAmount",
                value: 90m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 59,
                column: "FixedAmount",
                value: 95m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 60,
                column: "FixedAmount",
                value: 100m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 61,
                column: "FixedAmount",
                value: 105m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 62,
                column: "FixedAmount",
                value: 110m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 63,
                column: "FixedAmount",
                value: 115m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 64,
                column: "FixedAmount",
                value: 120m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 65,
                column: "FixedAmount",
                value: 125m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 66,
                column: "FixedAmount",
                value: 10m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 67,
                column: "FixedAmount",
                value: 20m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 68,
                column: "FixedAmount",
                value: 30m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 69,
                column: "FixedAmount",
                value: 40m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 70,
                column: "FixedAmount",
                value: 50m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 71,
                column: "FixedAmount",
                value: 60m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 72,
                column: "FixedAmount",
                value: 65m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 73,
                column: "FixedAmount",
                value: 70m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 74,
                column: "FixedAmount",
                value: 75m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 75,
                column: "FixedAmount",
                value: 80m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 76,
                column: "FixedAmount",
                value: 85m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 77,
                column: "FixedAmount",
                value: 90m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 78,
                column: "FixedAmount",
                value: 95m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 79,
                column: "FixedAmount",
                value: 100m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 80,
                column: "FixedAmount",
                value: 105m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 81,
                column: "FixedAmount",
                value: 110m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 82,
                column: "FixedAmount",
                value: 115m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 83,
                column: "FixedAmount",
                value: 120m);

            migrationBuilder.UpdateData(
                table: "CommissionTypes",
                keyColumn: "Id",
                keyValue: 84,
                column: "FixedAmount",
                value: 125m);
        }
    }
}
