using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class MakeHierarchyPathUnboundedDropPathIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GenealogyTree_HierarchyPath",
                table: "GenealogyTree");

            migrationBuilder.DropIndex(
                name: "IX_DualTeamTree_HierarchyPath",
                table: "DualTeamTree");

            migrationBuilder.AlterColumn<string>(
                name: "ParentMemberId",
                table: "GenealogyTree",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "HierarchyPath",
                table: "GenealogyTree",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "ParentMemberId",
                table: "DualTeamTree",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "HierarchyPath",
                table: "DualTeamTree",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.CreateIndex(
                name: "IX_GenealogyTree_ParentMemberId",
                table: "GenealogyTree",
                column: "ParentMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_DualTeamTree_ParentMemberId",
                table: "DualTeamTree",
                column: "ParentMemberId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GenealogyTree_ParentMemberId",
                table: "GenealogyTree");

            migrationBuilder.DropIndex(
                name: "IX_DualTeamTree_ParentMemberId",
                table: "DualTeamTree");

            migrationBuilder.AlterColumn<string>(
                name: "ParentMemberId",
                table: "GenealogyTree",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "HierarchyPath",
                table: "GenealogyTree",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ParentMemberId",
                table: "DualTeamTree",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "HierarchyPath",
                table: "DualTeamTree",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_GenealogyTree_HierarchyPath",
                table: "GenealogyTree",
                column: "HierarchyPath");

            migrationBuilder.CreateIndex(
                name: "IX_DualTeamTree_HierarchyPath",
                table: "DualTeamTree",
                column: "HierarchyPath");
        }
    }
}
