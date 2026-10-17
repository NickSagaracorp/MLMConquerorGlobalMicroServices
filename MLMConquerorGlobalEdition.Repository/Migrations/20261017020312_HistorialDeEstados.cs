using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class HistorialDeEstados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Plan J (R2): no estrechar la columna si algún valor no cabe; mejor fallar el despliegue
            // que truncar. DATALENGTH y no LEN: LEN no cuenta los espacios finales.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [MemberStatusHistories] WHERE DATALENGTH([MemberId]) / 2 > 450)
    THROW 51003, N'MemberStatusHistories.MemberId tiene valores de mas de 450 caracteres: revisar antes de migrar (Plan J, Tarea 27).', 1;");

            migrationBuilder.AlterColumn<string>(
                name: "MemberId",
                table: "MemberStatusHistories",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "MembershipStatusHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SubscriptionId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    MembershipLevelId = table.Column<int>(type: "int", nullable: false),
                    OldStatus = table.Column<int>(type: "int", nullable: true),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipStatusHistories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberStatusHistories_ChangedAt",
                table: "MemberStatusHistories",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MemberStatusHistories_MemberId_ChangedAt",
                table: "MemberStatusHistories",
                columns: new[] { "MemberId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MembershipStatusHistories_ChangedAt",
                table: "MembershipStatusHistories",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipStatusHistories_MemberId_ChangedAt",
                table: "MembershipStatusHistories",
                columns: new[] { "MemberId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MembershipStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_MemberStatusHistories_ChangedAt",
                table: "MemberStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_MemberStatusHistories_MemberId_ChangedAt",
                table: "MemberStatusHistories");

            migrationBuilder.AlterColumn<string>(
                name: "MemberId",
                table: "MemberStatusHistories",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);
        }
    }
}
