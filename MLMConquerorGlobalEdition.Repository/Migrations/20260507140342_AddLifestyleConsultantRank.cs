using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddLifestyleConsultantRank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "RankDefinitions",
                columns: new[] { "Id", "CertificateTemplateUrl", "CreatedBy", "CreationDate", "Description", "LastUpdateBy", "LastUpdateDate", "Name", "SortOrder", "Status" },
                values: new object[] { 20, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Default starting rank — no requirements. Earned automatically on signup.", null, null, "Lifestyle Consultant", 0, 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RankDefinitions",
                keyColumn: "Id",
                keyValue: 20);
        }
    }
}
