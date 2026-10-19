using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class CalificadosDelBinario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LeftLegQualified",
                table: "DualTeamTree",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RightLegQualified",
                table: "DualTeamTree",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SelfQualified",
                table: "DualTeamTree",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "OwnChange",
                table: "DualLegDeltas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "QualifiedCount",
                table: "DualLegDeltas",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeftLegQualified",
                table: "DualTeamTree");

            migrationBuilder.DropColumn(
                name: "RightLegQualified",
                table: "DualTeamTree");

            migrationBuilder.DropColumn(
                name: "SelfQualified",
                table: "DualTeamTree");

            migrationBuilder.DropColumn(
                name: "OwnChange",
                table: "DualLegDeltas");

            migrationBuilder.DropColumn(
                name: "QualifiedCount",
                table: "DualLegDeltas");
        }
    }
}
