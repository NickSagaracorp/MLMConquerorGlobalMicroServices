using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenCategoryAndProductRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TokenTypeProducts_TokenTypeId_ProductId",
                table: "TokenTypeProducts");

            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "TokenTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "QuantityGranted",
                table: "TokenTypeProducts",
                type: "int",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "TokenTypeProducts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.InsertData(
                table: "TokenTypeProducts",
                columns: new[] { "Id", "CreatedBy", "CreationDate", "LastUpdateBy", "LastUpdateDate", "ProductId", "QuantityGranted", "TokenTypeId" },
                values: new object[,]
                {
                    { 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000001-prod-0000-0000-000000000001", 1, 2 },
                    { 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000005-prod-0000-0000-000000000005", 1, 8 },
                    { 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000005-prod-0000-0000-000000000005", 1, 88 },
                    { 4, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000005-prod-0000-0000-000000000005", 1, 64 },
                    { 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000002-prod-0000-0000-000000000002", 1, 64 },
                    { 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000005-prod-0000-0000-000000000005", 1, 65 },
                    { 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000002-prod-0000-0000-000000000002", 1, 65 },
                    { 8, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000005-prod-0000-0000-000000000005", 1, 86 },
                    { 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000002-prod-0000-0000-000000000002", 1, 86 },
                    { 10, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000005-prod-0000-0000-000000000005", 1, 5 },
                    { 11, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 5 },
                    { 12, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000005-prod-0000-0000-000000000005", 1, 11 },
                    { 13, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 11 },
                    { 14, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000005-prod-0000-0000-000000000005", 1, 71 },
                    { 15, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 71 },
                    { 16, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000005-prod-0000-0000-000000000005", 1, 72 },
                    { 17, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 72 },
                    { 18, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000005-prod-0000-0000-000000000005", 1, 81 },
                    { 19, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 81 },
                    { 20, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000005-prod-0000-0000-000000000005", 1, 82 },
                    { 21, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 82 },
                    { 22, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000005-prod-0000-0000-000000000005", 1, 98 },
                    { 23, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 98 },
                    { 24, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000005-prod-0000-0000-000000000005", 1, 69 },
                    { 25, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 69 },
                    { 26, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000004-prod-0000-0000-000000000004", 1, 69 },
                    { 27, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000005-prod-0000-0000-000000000005", 1, 70 },
                    { 28, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 70 },
                    { 29, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000004-prod-0000-0000-000000000004", 1, 70 },
                    { 30, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000005-prod-0000-0000-000000000005", 1, 73 },
                    { 31, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 73 },
                    { 32, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000004-prod-0000-0000-000000000004", 1, 73 },
                    { 33, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000005-prod-0000-0000-000000000005", 1, 74 },
                    { 34, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 74 },
                    { 35, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000004-prod-0000-0000-000000000004", 1, 74 },
                    { 36, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000005-prod-0000-0000-000000000005", 1, 83 },
                    { 37, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 83 },
                    { 38, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000004-prod-0000-0000-000000000004", 1, 83 },
                    { 39, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000005-prod-0000-0000-000000000005", 1, 84 },
                    { 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 84 },
                    { 41, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000004-prod-0000-0000-000000000004", 1, 84 },
                    { 42, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000005-prod-0000-0000-000000000005", 1, 99 },
                    { 43, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 99 },
                    { 44, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000004-prod-0000-0000-000000000004", 1, 99 },
                    { 45, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000002-prod-0000-0000-000000000002", 1, 13 },
                    { 46, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 16 },
                    { 47, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 19 },
                    { 48, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 80 },
                    { 49, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000004-prod-0000-0000-000000000004", 1, 80 },
                    { 50, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 89 },
                    { 51, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 90 },
                    { 52, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000004-prod-0000-0000-000000000004", 1, 90 },
                    { 53, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000002-prod-0000-0000-000000000002", 1, 92 },
                    { 54, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 6 },
                    { 55, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000005-prod-0000-0000-000000000005", 1, 94 },
                    { 56, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 94 },
                    { 57, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000005-prod-0000-0000-000000000005", 1, 96 },
                    { 58, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 96 },
                    { 59, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000004-prod-0000-0000-000000000004", 1, 96 },
                    { 60, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 3 },
                    { 61, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000002-prod-0000-0000-000000000002", 1, 4 },
                    { 62, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000005-prod-0000-0000-000000000005", 1, 23 },
                    { 63, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000005-prod-0000-0000-000000000005", 1, 10 }
                });

            migrationBuilder.InsertData(
                table: "TokenTypeProducts",
                columns: new[] { "Id", "CreatedBy", "CreationDate", "LastUpdateBy", "LastUpdateDate", "ProductId", "Role", "TokenTypeId" },
                values: new object[] { 64, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000001-prod-0000-0000-000000000001", 1, 56 });

            migrationBuilder.InsertData(
                table: "TokenTypeProducts",
                columns: new[] { "Id", "CreatedBy", "CreationDate", "LastUpdateBy", "LastUpdateDate", "ProductId", "QuantityGranted", "Role", "TokenTypeId" },
                values: new object[] { 65, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000002-prod-0000-0000-000000000002", 1, 2, 56 });

            migrationBuilder.InsertData(
                table: "TokenTypeProducts",
                columns: new[] { "Id", "CreatedBy", "CreationDate", "LastUpdateBy", "LastUpdateDate", "ProductId", "Role", "TokenTypeId" },
                values: new object[] { 66, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000001-prod-0000-0000-000000000001", 1, 59 });

            migrationBuilder.InsertData(
                table: "TokenTypeProducts",
                columns: new[] { "Id", "CreatedBy", "CreationDate", "LastUpdateBy", "LastUpdateDate", "ProductId", "QuantityGranted", "Role", "TokenTypeId" },
                values: new object[] { 67, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 2, 59 });

            migrationBuilder.InsertData(
                table: "TokenTypeProducts",
                columns: new[] { "Id", "CreatedBy", "CreationDate", "LastUpdateBy", "LastUpdateDate", "ProductId", "Role", "TokenTypeId" },
                values: new object[] { 68, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000002-prod-0000-0000-000000000002", 1, 62 });

            migrationBuilder.InsertData(
                table: "TokenTypeProducts",
                columns: new[] { "Id", "CreatedBy", "CreationDate", "LastUpdateBy", "LastUpdateDate", "ProductId", "QuantityGranted", "Role", "TokenTypeId" },
                values: new object[] { 69, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 2, 62 });

            migrationBuilder.InsertData(
                table: "TokenTypeProducts",
                columns: new[] { "Id", "CreatedBy", "CreationDate", "LastUpdateBy", "LastUpdateDate", "ProductId", "Role", "TokenTypeId" },
                values: new object[] { 70, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000003-prod-0000-0000-000000000003", 1, 66 });

            migrationBuilder.InsertData(
                table: "TokenTypeProducts",
                columns: new[] { "Id", "CreatedBy", "CreationDate", "LastUpdateBy", "LastUpdateDate", "ProductId", "QuantityGranted", "Role", "TokenTypeId" },
                values: new object[] { 71, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "00000004-prod-0000-0000-000000000004", 1, 2, 66 });

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 1,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 2,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 3,
                column: "Category",
                value: 3);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 4,
                column: "Category",
                value: 3);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 5,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 6,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 7,
                column: "Category",
                value: 5);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 8,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 9,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 10,
                column: "Category",
                value: 4);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 11,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 12,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 13,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 14,
                column: "Category",
                value: 5);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 15,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 16,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 17,
                column: "Category",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 19,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 21,
                column: "Category",
                value: 4);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 22,
                column: "Category",
                value: 3);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 23,
                column: "Category",
                value: 4);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 24,
                column: "Category",
                value: 5);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 25,
                column: "Category",
                value: 3);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 26,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 27,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 28,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 29,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 30,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 31,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 32,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 33,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 34,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 35,
                column: "Category",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 36,
                column: "Category",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 37,
                column: "Category",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 38,
                column: "Category",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 39,
                column: "Category",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 40,
                column: "Category",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 41,
                column: "Category",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 42,
                column: "Category",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 43,
                column: "Category",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 44,
                column: "Category",
                value: 3);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 45,
                column: "Category",
                value: 3);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 46,
                column: "Category",
                value: 3);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 47,
                column: "Category",
                value: 3);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 48,
                column: "Category",
                value: 3);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 49,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 50,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 51,
                column: "Category",
                value: 3);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 52,
                column: "Category",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 53,
                column: "Category",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 54,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 55,
                column: "Category",
                value: 3);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 56,
                column: "Category",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 57,
                column: "Category",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 58,
                column: "Category",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 59,
                column: "Category",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 60,
                column: "Category",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 61,
                column: "Category",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 62,
                column: "Category",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 63,
                column: "Category",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 64,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 65,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 66,
                column: "Category",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 67,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 68,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 69,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 70,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 71,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 72,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 73,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 74,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 75,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 76,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 77,
                column: "Category",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 78,
                column: "Category",
                value: 4);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 79,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 80,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 81,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 82,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 83,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 84,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 85,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 86,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 87,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 88,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 89,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 90,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 91,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 92,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 93,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 94,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 95,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 96,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 97,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 98,
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TokenTypes",
                keyColumn: "Id",
                keyValue: 99,
                column: "Category",
                value: 1);

            migrationBuilder.CreateIndex(
                name: "IX_TokenTypeProducts_TokenTypeId_ProductId_Role",
                table: "TokenTypeProducts",
                columns: new[] { "TokenTypeId", "ProductId", "Role" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TokenTypeProducts_TokenTypes_TokenTypeId",
                table: "TokenTypeProducts",
                column: "TokenTypeId",
                principalTable: "TokenTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TokenTypeProducts_TokenTypes_TokenTypeId",
                table: "TokenTypeProducts");

            migrationBuilder.DropIndex(
                name: "IX_TokenTypeProducts_TokenTypeId_ProductId_Role",
                table: "TokenTypeProducts");

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 53);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 54);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 55);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 56);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 57);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 58);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 59);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 60);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 61);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 62);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 63);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 64);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 65);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 66);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 67);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 68);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 69);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 70);

            migrationBuilder.DeleteData(
                table: "TokenTypeProducts",
                keyColumn: "Id",
                keyValue: 71);

            migrationBuilder.DropColumn(
                name: "Category",
                table: "TokenTypes");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "TokenTypeProducts");

            migrationBuilder.AlterColumn<int>(
                name: "QuantityGranted",
                table: "TokenTypeProducts",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_TokenTypeProducts_TokenTypeId_ProductId",
                table: "TokenTypeProducts",
                columns: new[] { "TokenTypeId", "ProductId" },
                unique: true);
        }
    }
}
